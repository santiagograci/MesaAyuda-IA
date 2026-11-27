using MesaAyuda.Application.Common;
using MesaAyuda.Application.DTOs;

namespace MesaAyuda.Application.Interfaces;

/// <summary>
/// Contrato de casos de uso para gestión integral de tickets, máquina de estados y soporte RAG.
/// </summary>
public interface ICasoService
{
    /// <summary>Lista casos paginados filtrando por propiedad cuando el rol es Cliente.</summary>
    Task<PagedResult<CasoResumenDto>> ListarCasosAsync(
        Guid usuarioId,
        string rol,
        string? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene el detalle completo de un caso validando propiedad si el usuario es Cliente.</summary>
    Task<CasoDetalleDto> ObtenerPorIdAsync(
        Guid id,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Crea un nuevo caso en estado inicial Abierto.</summary>
    Task<CasoDetalleDto> CrearCasoAsync(
        CrearCasoRequest request,
        Guid clienteId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza título, descripción y prioridad de un caso no cerrado.</summary>
    Task<CasoDetalleDto> ActualizarCasoAsync(
        Guid id,
        ActualizarCasoRequest request,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Elimina un caso de soporte (operación restringida a rol Analista).</summary>
    Task EliminarCasoAsync(
        Guid id,
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Ejecuta la máquina de estados del dominio sobre un caso validando rol y propiedad.</summary>
    Task<CasoDetalleDto> CambiarEstadoAsync(
        Guid id,
        string nuevoEstado,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Registra una respuesta humana y la publica en el historial del caso.</summary>
    Task<RespuestaDto> AgregarRespuestaHumanaAsync(
        Guid id,
        CrearRespuestaRequest request,
        Guid usuarioId,
        string nombreUsuario,
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Consulta al servicio RAG y guarda una sugerencia IA pendiente de aprobación.</summary>
    Task<RespuestaDto> GenerarSugerenciaIAAsync(
        Guid id,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Aprueba y opcionalmente edita una sugerencia IA para publicarla oficialmente.</summary>
    Task<RespuestaDto> AprobarSugerenciaIAAsync(
        Guid casoId,
        Guid respuestaId,
        AprobarSugerenciaRequest request,
        string nombreAnalista,
        CancellationToken cancellationToken = default);
}
