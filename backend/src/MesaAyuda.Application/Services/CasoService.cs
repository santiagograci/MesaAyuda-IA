using System.Text.Json;
using FluentValidation;
using MesaAyuda.Application.Common;
using MesaAyuda.Application.DTOs;
using MesaAyuda.Application.Interfaces;
using MesaAyuda.Domain.Entities;
using MesaAyuda.Domain.Enums;
using MesaAyuda.Domain.Exceptions;
using MesaAyuda.Domain.Interfaces;

namespace MesaAyuda.Application.Services;

/// <summary>
/// Orquesta los casos de uso de tickets: CRUD paginado, control de acceso por rol,
/// máquina de estados del dominio y generación/aprobación de respuestas RAG.
/// </summary>
public class CasoService : ICasoService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICasoRepository _casoRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRagServiceClient _ragServiceClient;
    private readonly IValidator<CrearCasoRequest> _crearValidator;
    private readonly IValidator<ActualizarCasoRequest> _actualizarValidator;
    private readonly IValidator<CrearRespuestaRequest> _respuestaValidator;
    private readonly IValidator<AprobarSugerenciaRequest> _aprobarValidator;

    /// <summary>
    /// Inicializa el servicio de gestión de casos con repositorios, cliente RAG y validadores.
    /// </summary>
    public CasoService(
        ICasoRepository casoRepository,
        IUsuarioRepository usuarioRepository,
        IUnitOfWork unitOfWork,
        IRagServiceClient ragServiceClient,
        IValidator<CrearCasoRequest> crearValidator,
        IValidator<ActualizarCasoRequest> actualizarValidator,
        IValidator<CrearRespuestaRequest> respuestaValidator,
        IValidator<AprobarSugerenciaRequest> aprobarValidator)
    {
        _casoRepository = casoRepository;
        _usuarioRepository = usuarioRepository;
        _unitOfWork = unitOfWork;
        _ragServiceClient = ragServiceClient;
        _crearValidator = crearValidator;
        _actualizarValidator = actualizarValidator;
        _respuestaValidator = respuestaValidator;
        _aprobarValidator = aprobarValidator;
    }

    /// <inheritdoc />
    public async Task<PagedResult<CasoResumenDto>> ListarCasosAsync(
        Guid usuarioId,
        string rol,
        string? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        EstadoCaso? estadoFiltro = null;
        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!Enum.TryParse<EstadoCaso>(estado.Trim(), ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                throw new DomainException($"Estado de caso no válido: '{estado}'.");
            }

            estadoFiltro = parsed;
        }

        var esCliente = string.Equals(rol, nameof(RolUsuario.Cliente), StringComparison.OrdinalIgnoreCase);
        Guid? clienteFiltro = esCliente ? usuarioId : null;

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await _casoRepository.GetPagedAsync(
            clienteId: clienteFiltro,
            estado: estadoFiltro,
            prioridad: null,
            searchTerm: null,
            page: safePage,
            pageSize: safePageSize,
            cancellationToken: cancellationToken);

        var dtos = items.Select(c => new CasoResumenDto(
            Id: c.Id,
            Titulo: c.Titulo,
            Prioridad: c.Prioridad.ToString(),
            Estado: c.Estado.ToString(),
            FechaCreacion: c.FechaCreacion,
            ClienteNombre: c.Cliente?.Nombre ?? "Cliente ERP",
            TotalRespuestas: esCliente
                ? c.Respuestas.Count(r => !r.EsGeneradaPorIA || r.AprobadaPorAnalista)
                : c.Respuestas.Count,
            TieneSugerenciaPendiente: !esCliente && c.Respuestas.Any(r => r.EsGeneradaPorIA && !r.AprobadaPorAnalista)
        )).ToList();

        return new PagedResult<CasoResumenDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = safePage,
            PageSize = safePageSize
        };
    }

    /// <inheritdoc />
    public async Task<CasoDetalleDto> ObtenerPorIdAsync(
        Guid id,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default)
    {
        var caso = await GetCasoOrThrowAsync(id, cancellationToken);
        EnsureOwnershipOrAnalyst(caso, usuarioId, rol);
        var esAnalista = string.Equals(rol, nameof(RolUsuario.Analista), StringComparison.OrdinalIgnoreCase);
        return MapToDetalleDto(caso, soloPublicadas: !esAnalista);
    }

    /// <inheritdoc />
    public async Task<CasoDetalleDto> CrearCasoAsync(
        CrearCasoRequest request,
        Guid clienteId,
        CancellationToken cancellationToken = default)
    {
        await _crearValidator.ValidateAndThrowAsync(request, cancellationToken);

        var cliente = await _usuarioRepository.GetByIdAsync(clienteId, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el usuario cliente '{clienteId}'.");

        var prioridad = Enum.Parse<PrioridadCaso>(request.Prioridad.Trim(), ignoreCase: true);
        var caso = new Caso(request.Titulo, request.Descripcion, prioridad, clienteId);

        await _casoRepository.AddAsync(caso, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDetalleDto(caso, cliente.Nombre);
    }

    /// <inheritdoc />
    public async Task<CasoDetalleDto> ActualizarCasoAsync(
        Guid id,
        ActualizarCasoRequest request,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default)
    {
        await _actualizarValidator.ValidateAndThrowAsync(request, cancellationToken);

        var caso = await GetCasoOrThrowAsync(id, cancellationToken);
        EnsureOwnershipOrAnalyst(caso, usuarioId, rol);

        var prioridad = Enum.Parse<PrioridadCaso>(request.Prioridad.Trim(), ignoreCase: true);
        caso.Actualizar(request.Titulo, request.Descripcion, prioridad);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDetalleDto(caso);
    }

    /// <inheritdoc />
    public async Task EliminarCasoAsync(
        Guid id,
        string rol,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(rol, nameof(RolUsuario.Analista), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Solo un Analista puede eliminar casos.");

        var caso = await GetCasoOrThrowAsync(id, cancellationToken);
        _casoRepository.Remove(caso);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CasoDetalleDto> CambiarEstadoAsync(
        Guid id,
        string nuevoEstado,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<EstadoCaso>(nuevoEstado?.Trim(), ignoreCase: true, out var estadoDestino)
            || !Enum.IsDefined(estadoDestino))
        {
            throw new DomainException($"Estado destino no válido: '{nuevoEstado}'.");
        }

        var caso = await GetCasoOrThrowAsync(id, cancellationToken);
        EnsureOwnershipOrAnalyst(caso, usuarioId, rol);
        caso.CambiarEstado(estadoDestino);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var esAnalista = string.Equals(rol, nameof(RolUsuario.Analista), StringComparison.OrdinalIgnoreCase);
        return MapToDetalleDto(caso, soloPublicadas: !esAnalista);
    }

    /// <inheritdoc />
    public async Task<RespuestaDto> AgregarRespuestaHumanaAsync(
        Guid id,
        CrearRespuestaRequest request,
        Guid usuarioId,
        string nombreUsuario,
        string rol,
        CancellationToken cancellationToken = default)
    {
        await _respuestaValidator.ValidateAndThrowAsync(request, cancellationToken);

        var caso = await GetCasoOrThrowAsync(id, cancellationToken);
        EnsureOwnershipOrAnalyst(caso, usuarioId, rol);

        var respuesta = caso.AgregarRespuestaHumana(request.Texto, nombreUsuario);
        await _casoRepository.AddRespuestaAsync(respuesta, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToRespuestaDto(respuesta);
    }

    /// <inheritdoc />
    public async Task<RespuestaDto> GenerarSugerenciaIAAsync(
        Guid id,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default)
    {
        var caso = await GetCasoOrThrowAsync(id, cancellationToken);
        EnsureOwnershipOrAnalyst(caso, usuarioId, rol);

        if (caso.Estado == EstadoCaso.Cerrado)
            throw new DomainException("No se pueden generar sugerencias de IA en un caso Cerrado.");

        var consulta = $"{caso.Titulo}. {caso.Descripcion}";
        var ragResponse = await _ragServiceClient.ConsultarSugerenciaAsync(consulta, cancellationToken);

        var fuentesJson = JsonSerializer.Serialize(ragResponse.Fuentes ?? new List<FuenteCitadaDto>(), JsonOptions);
        var sugerencia = caso.AgregarSugerenciaIA(ragResponse.Respuesta, fuentesJson);

        await _casoRepository.AddRespuestaAsync(sugerencia, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToRespuestaDto(sugerencia);
    }

    /// <inheritdoc />
    public async Task<RespuestaDto> AprobarSugerenciaIAAsync(
        Guid casoId,
        Guid respuestaId,
        AprobarSugerenciaRequest request,
        string nombreAnalista,
        CancellationToken cancellationToken = default)
    {
        await _aprobarValidator.ValidateAndThrowAsync(request, cancellationToken);

        var caso = await GetCasoOrThrowAsync(casoId, cancellationToken);
        var respuesta = caso.AprobarSugerenciaIA(respuestaId, request.TextoFinal, nombreAnalista);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToRespuestaDto(respuesta);
    }

    private async Task<Caso> GetCasoOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _casoRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el caso con identificador '{id}'.");
    }

    private static void EnsureOwnershipOrAnalyst(Caso caso, Guid usuarioId, string rol)
    {
        var esAnalista = string.Equals(rol, nameof(RolUsuario.Analista), StringComparison.OrdinalIgnoreCase);
        if (!esAnalista && caso.ClienteId != usuarioId)
            throw new UnauthorizedAccessException("No tiene permisos para acceder o modificar un caso de otro cliente.");
    }

    private static CasoDetalleDto MapToDetalleDto(
        Caso caso,
        string? clienteNombreOverride = null,
        bool soloPublicadas = false)
    {
        var respuestas = caso.Respuestas
            .Where(r => !soloPublicadas || !r.EsGeneradaPorIA || r.AprobadaPorAnalista)
            .OrderBy(r => r.FechaCreacion)
            .Select(MapToRespuestaDto)
            .ToList();

        return new CasoDetalleDto(
            Id: caso.Id,
            Titulo: caso.Titulo,
            Descripcion: caso.Descripcion,
            Prioridad: caso.Prioridad.ToString(),
            Estado: caso.Estado.ToString(),
            FechaCreacion: caso.FechaCreacion,
            ClienteId: caso.ClienteId,
            ClienteNombre: clienteNombreOverride ?? caso.Cliente?.Nombre ?? "Cliente ERP",
            Respuestas: respuestas);
    }

    private static RespuestaDto MapToRespuestaDto(Respuesta respuesta)
    {
        List<FuenteCitadaDto> fuentes;
        try
        {
            fuentes = JsonSerializer.Deserialize<List<FuenteCitadaDto>>(respuesta.FuentesCitadasJson, JsonOptions)
                ?? new List<FuenteCitadaDto>();
        }
        catch (JsonException)
        {
            fuentes = new List<FuenteCitadaDto>();
        }

        return new RespuestaDto(
            Id: respuesta.Id,
            Texto: respuesta.Texto,
            EsGeneradaPorIA: respuesta.EsGeneradaPorIA,
            AprobadaPorAnalista: respuesta.AprobadaPorAnalista,
            AutorNombre: respuesta.AutorNombre,
            FuentesCitadas: fuentes,
            FechaCreacion: respuesta.FechaCreacion);
    }
}
