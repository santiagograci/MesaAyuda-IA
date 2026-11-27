using MesaAyuda.Application.Common;
using MesaAyuda.Application.DTOs;

namespace MesaAyuda.Application.Interfaces;

/// <summary>
/// Contrato de casos de uso para gestión integral de tickets, máquina de estados y soporte RAG.
/// </summary>
public interface ICasoService
{
    /// <summary>Lista casos paginados filtrando por propiedad, estado, prioridad y búsqueda textual.</summary>
    Task<PagedResult<CasoResumenDto>> ListarCasosAsync(
        Guid usuarioId,
        string rol,
        string? estado,
        int page,
        int pageSize,
        string? prioridad = null,
        string? busqueda = null,
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

    /// <summary>Envía un caso de soporte a la papelera mediante borrado lógico (rol Analista).</summary>
    Task EliminarCasoAsync(
        Guid id,
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Lista todos los casos que se encuentran en la papelera de reciclaje (exclusivo Analista).</summary>
    Task<IReadOnlyList<CasoEliminadoDto>> ListarPapeleraAsync(
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Restaura un caso eliminado desde la papelera hacia la bandeja activa (exclusivo Analista).</summary>
    Task<CasoDetalleDto> RestaurarCasoAsync(
        Guid id,
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Recrea los casos de demostración ausentes sin duplicar los activos (exclusivo Analista).</summary>
    Task<RestaurarDemoResponse> RestaurarDatosDemoAsync(
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Ejecuta la máquina de estados del dominio sobre un caso validando rol y propiedad.</summary>
    Task<CasoDetalleDto> CambiarEstadoAsync(
        Guid id,
        string nuevoEstado,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Escala o ajusta la prioridad de un caso registrando justificación obligatoria en su historial.</summary>
    Task<CasoDetalleDto> CambiarPrioridadAsync(
        Guid id,
        CambiarPrioridadRequest request,
        Guid usuarioId,
        string rol,
        string? nombreUsuario = null,
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

    /// <summary>Consulta al servicio RAG preparando fuentes y respuesta antes del stream SSE.</summary>
    Task<SugerenciaIaPreparada> ConsultarRagParaCasoAsync(
        Guid id,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Persiste la sugerencia IA generada tras finalizar la transmisión SSE.</summary>
    Task<CasoDetalleDto> PersistirSugerenciaIaAsync(
        Guid id,
        SugerenciaIaPreparada sugerencia,
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Calcula métricas operativas, CSAT y consumo FinOps de IA para el usuario o globalmente.</summary>
    Task<MetricasMesaAyudaDto> ObtenerMetricasAsync(
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default);

    /// <summary>Consulta sugerencias preventivas del motor RAG sin persistir un caso todavía.</summary>
    Task<DeflexionResponse> ConsultarDeflexionAsync(
        DeflexionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Registra la encuesta de satisfacción CSAT del cliente dueño en un caso resuelto o cerrado.</summary>
    Task<CasoDetalleDto> RegistrarCsatAsync(
        Guid id,
        RegistrarCsatRequest request,
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
