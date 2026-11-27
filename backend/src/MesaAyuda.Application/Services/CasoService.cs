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
        string? prioridad = null,
        string? busqueda = null,
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

        PrioridadCaso? prioridadFiltro = null;
        if (!string.IsNullOrWhiteSpace(prioridad))
        {
            if (!Enum.TryParse<PrioridadCaso>(prioridad.Trim(), ignoreCase: true, out var parsedPrio)
                || !Enum.IsDefined(parsedPrio))
            {
                throw new DomainException($"Prioridad de caso no válida: '{prioridad}'.");
            }

            prioridadFiltro = parsedPrio;
        }

        var esCliente = string.Equals(rol, nameof(RolUsuario.Cliente), StringComparison.OrdinalIgnoreCase);
        Guid? clienteFiltro = esCliente ? usuarioId : null;

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await _casoRepository.GetPagedAsync(
            clienteId: clienteFiltro,
            estado: estadoFiltro,
            prioridad: prioridadFiltro,
            searchTerm: busqueda,
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
            TieneSugerenciaPendiente: !esCliente && c.Respuestas.Any(r => r.EsGeneradaPorIA && !r.AprobadaPorAnalista),
            FechaLimiteSla: c.CalcularFechaLimiteSla(),
            EstadoSla: c.EvaluarEstadoSla()
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
        caso.MarcarComoEliminado();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CasoEliminadoDto>> ListarPapeleraAsync(
        string rol,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(rol, nameof(RolUsuario.Analista), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Solo un Analista puede consultar la papelera de reciclaje.");

        var eliminados = await _casoRepository.ListarEliminadosAsync(cancellationToken);
        return eliminados
            .Select(c => new CasoEliminadoDto(
                Id: c.Id,
                Titulo: c.Titulo,
                Prioridad: c.Prioridad.ToString(),
                Estado: c.Estado.ToString(),
                FechaCreacion: c.FechaCreacion,
                FechaEliminacion: c.FechaEliminacion,
                ClienteNombre: c.Cliente?.Nombre ?? "Cliente ERP"))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CasoDetalleDto> RestaurarCasoAsync(
        Guid id,
        string rol,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(rol, nameof(RolUsuario.Analista), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Solo un Analista puede restaurar casos desde la papelera.");

        var caso = await _casoRepository.GetByIdIncludingDeletedAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el caso con identificador '{id}'.");

        caso.Restaurar();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDetalleDto(caso);
    }

    /// <inheritdoc />
    public async Task<RestaurarDemoResponse> RestaurarDatosDemoAsync(
        string rol,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(rol, nameof(RolUsuario.Analista), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Solo un Analista puede restaurar los casos de demostración.");

        var usuarios = await _usuarioRepository.GetAllAsync(cancellationToken);
        var cliente1 = usuarios.FirstOrDefault(u => string.Equals(u.Email, "cliente@empresa.com", StringComparison.OrdinalIgnoreCase))
            ?? usuarios.FirstOrDefault(u => u.Rol == RolUsuario.Cliente)
            ?? throw new KeyNotFoundException("No existen usuarios con rol Cliente para asociar los casos demo.");
        var cliente2 = usuarios.FirstOrDefault(u => string.Equals(u.Email, "cliente2@empresa.com", StringComparison.OrdinalIgnoreCase))
            ?? cliente1;
        var analista = usuarios.FirstOrDefault(u => string.Equals(u.Email, "analista@ofima.com", StringComparison.OrdinalIgnoreCase))
            ?? usuarios.FirstOrDefault(u => u.Rol == RolUsuario.Analista);

        var plantillaDemo = DemoCasosCatalog.CreateDemoCases(
            cliente1.Id,
            cliente2.Id,
            analista?.Nombre ?? "Analista Soporte Ofima");

        var creados = 0;
        foreach (var demo in plantillaDemo)
        {
            if (await _casoRepository.ExistsActiveByTitleAsync(demo.Titulo, cancellationToken))
                continue;

            await _casoRepository.AddAsync(demo, cancellationToken);
            creados++;
        }

        if (creados > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        var mensaje = creados > 0
            ? $"Se restauraron {creados} caso(s) de demostración en la bandeja activa."
            : "Los 3 casos de demostración ya se encuentran activos en la bandeja.";

        return new RestaurarDemoResponse(creados, mensaje);
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
    public async Task<CasoDetalleDto> CambiarPrioridadAsync(
        Guid id,
        CambiarPrioridadRequest request,
        Guid usuarioId,
        string rol,
        string? nombreUsuario = null,
        CancellationToken cancellationToken = default)
    {
        var justificacion = request.Justificacion?.Trim() ?? string.Empty;
        if (justificacion.Length < 10)
            throw new DomainException("La justificación debe tener al menos 10 caracteres.");

        var caso = await GetCasoOrThrowAsync(id, cancellationToken);
        EnsureOwnershipOrAnalyst(caso, usuarioId, rol);

        var prioridadAnterior = caso.Prioridad;
        caso.CambiarPrioridad(request.NuevaPrioridad, rol);

        var autorNombre = !string.IsNullOrWhiteSpace(nombreUsuario)
            ? nombreUsuario.Trim()
            : (await _usuarioRepository.GetByIdAsync(usuarioId, cancellationToken))?.Nombre ?? rol;

        var textoAuditoria =
            $"🔺 [Cambio de Prioridad: {prioridadAnterior} ➔ {request.NuevaPrioridad}] Justificación: {justificacion}";

        var auditoria = caso.RegistrarAuditoria(textoAuditoria, autorNombre);
        await _casoRepository.AddRespuestaAsync(auditoria, cancellationToken);
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
        var sugerencia = caso.AgregarSugerenciaIA(ragResponse.Respuesta, fuentesJson, ragResponse.TokensUsados);

        await _casoRepository.AddRespuestaAsync(sugerencia, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToRespuestaDto(sugerencia);
    }

    /// <inheritdoc />
    public async Task<SugerenciaIaPreparada> ConsultarRagParaCasoAsync(
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

        return new SugerenciaIaPreparada(
            Respuesta: ragResponse.Respuesta,
            Fuentes: ragResponse.Fuentes ?? new List<FuenteCitadaDto>(),
            TokensUsados: ragResponse.TokensUsados);
    }

    /// <inheritdoc />
    public async Task<CasoDetalleDto> PersistirSugerenciaIaAsync(
        Guid id,
        SugerenciaIaPreparada sugerencia,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default)
    {
        var caso = await GetCasoOrThrowAsync(id, cancellationToken);
        EnsureOwnershipOrAnalyst(caso, usuarioId, rol);

        var fuentesJson = JsonSerializer.Serialize(sugerencia.Fuentes ?? new List<FuenteCitadaDto>(), JsonOptions);
        var entity = caso.AgregarSugerenciaIA(sugerencia.Respuesta, fuentesJson, sugerencia.TokensUsados);

        await _casoRepository.AddRespuestaAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var esAnalista = string.Equals(rol, nameof(RolUsuario.Analista), StringComparison.OrdinalIgnoreCase);
        return MapToDetalleDto(caso, soloPublicadas: !esAnalista);
    }

    /// <inheritdoc />
    public async Task<MetricasMesaAyudaDto> ObtenerMetricasAsync(
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default)
    {
        var esAnalista = string.Equals(rol, nameof(RolUsuario.Analista), StringComparison.OrdinalIgnoreCase);
        var filtroCliente = esAnalista ? (Guid?)null : usuarioId;

        var casosActivos = await _casoRepository.ListarActivosConRespuestasAsync(filtroCliente, cancellationToken);

        var totalCasos = casosActivos.Count;
        var abiertos = casosActivos.Count(c => c.Estado == EstadoCaso.Abierto);
        var enProceso = casosActivos.Count(c => c.Estado == EstadoCaso.EnProceso);
        var resueltos = casosActivos.Count(c => c.Estado == EstadoCaso.Resuelto);
        var cerrados = casosActivos.Count(c => c.Estado == EstadoCaso.Cerrado);
        var criticosActivos = casosActivos.Count(c =>
            c.Prioridad == PrioridadCaso.Critica &&
            c.Estado != EstadoCaso.Resuelto &&
            c.Estado != EstadoCaso.Cerrado);

        var respuestasIa = casosActivos.SelectMany(c => c.Respuestas).Where(r => r.EsGeneradaPorIA).ToList();
        var generadas = respuestasIa.Count;
        var aprobadas = respuestasIa.Count(r => r.AprobadaPorAnalista);
        var tasaAdopcion = generadas == 0 ? 0.0 : Math.Round((double)aprobadas * 100.0 / generadas, 1);
        var tokensTotales = respuestasIa.Sum(r => r.TokensUsados);

        var calificados = casosActivos.Where(c => c.CalificacionCsat.HasValue).ToList();
        var totalCsat = calificados.Count;
        double? promedioCsat = totalCsat == 0
            ? null
            : Math.Round(calificados.Average(c => c.CalificacionCsat!.Value), 1);

        return new MetricasMesaAyudaDto(
            TotalCasos: totalCasos,
            CasosAbiertos: abiertos,
            CasosEnProceso: enProceso,
            CasosResueltos: resueltos,
            CasosCerrados: cerrados,
            CasosCriticosActivos: criticosActivos,
            SugerenciasIaGeneradas: generadas,
            SugerenciasIaAprobadas: aprobadas,
            TasaAdopcionIaPorcentaje: tasaAdopcion,
            TokensTotalesConsumidos: tokensTotales,
            PromedioCsat: promedioCsat,
            TotalEncuestasCsat: totalCsat);
    }

    /// <inheritdoc />
    public async Task<DeflexionResponse> ConsultarDeflexionAsync(
        DeflexionRequest request,
        CancellationToken cancellationToken = default)
    {
        var consulta = $"{request.Titulo?.Trim()} {request.Descripcion?.Trim()}".Trim();
        if (consulta.Length < 10)
            return new DeflexionResponse(false, string.Empty, new List<FuenteCitadaDto>());

        var ragResponse = await _ragServiceClient.ConsultarSugerenciaAsync(consulta, cancellationToken);
        var fuentes = (ragResponse.Fuentes ?? new List<FuenteCitadaDto>()).Take(2).ToList();

        if (!ragResponse.Anclado || fuentes.Count == 0)
            return new DeflexionResponse(false, string.Empty, new List<FuenteCitadaDto>());

        return new DeflexionResponse(true, ragResponse.Respuesta, fuentes);
    }

    /// <inheritdoc />
    public async Task<CasoDetalleDto> RegistrarCsatAsync(
        Guid id,
        RegistrarCsatRequest request,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(rol, nameof(RolUsuario.Cliente), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Solo el usuario Cliente propietario puede calificar el caso.");

        var caso = await GetCasoOrThrowAsync(id, cancellationToken);
        if (caso.ClienteId != usuarioId)
            throw new UnauthorizedAccessException("Solo el cliente propietario del caso puede enviar la encuesta CSAT.");

        caso.RegistrarCsat(request.Calificacion, request.Comentario);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDetalleDto(caso, soloPublicadas: true);
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
            Respuestas: respuestas,
            FechaLimiteSla: caso.CalcularFechaLimiteSla(),
            EstadoSla: caso.EvaluarEstadoSla(),
            CalificacionCsat: caso.CalificacionCsat,
            ComentarioCsat: caso.ComentarioCsat);
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
