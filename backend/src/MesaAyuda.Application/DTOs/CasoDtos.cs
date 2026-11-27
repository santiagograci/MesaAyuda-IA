using System.ComponentModel.DataAnnotations;
using MesaAyuda.Domain.Enums;

namespace MesaAyuda.Application.DTOs;

/// <summary>
/// Fragmento documental recuperado del corpus ERP que respalda una sugerencia de IA.
/// </summary>
public record FuenteCitadaDto(
    string Documento,
    string Seccion,
    string Fragmento,
    double Puntaje);

/// <summary>
/// Representación de una respuesta humana o sugerencia de IA asociada a un caso.
/// </summary>
public record RespuestaDto(
    Guid Id,
    string Texto,
    bool EsGeneradaPorIA,
    bool AprobadaPorAnalista,
    string AutorNombre,
    List<FuenteCitadaDto> FuentesCitadas,
    DateTime FechaCreacion);

/// <summary>
/// Vista resumida de un caso para listados paginados.
/// </summary>
public record CasoResumenDto(
    Guid Id,
    string Titulo,
    string Prioridad,
    string Estado,
    DateTime FechaCreacion,
    string ClienteNombre,
    int TotalRespuestas,
    bool TieneSugerenciaPendiente,
    DateTime FechaLimiteSla,
    string EstadoSla);

/// <summary>
/// Vista detallada de un caso incluyendo su historial completo de respuestas y fuentes.
/// </summary>
public record CasoDetalleDto(
    Guid Id,
    string Titulo,
    string Descripcion,
    string Prioridad,
    string Estado,
    DateTime FechaCreacion,
    Guid ClienteId,
    string ClienteNombre,
    List<RespuestaDto> Respuestas,
    DateTime FechaLimiteSla,
    string EstadoSla,
    int? CalificacionCsat,
    string? ComentarioCsat);

/// <summary>
/// Solicitud para crear un nuevo caso de soporte técnico.
/// </summary>
public record CrearCasoRequest(string Titulo, string Descripcion, string Prioridad);

/// <summary>
/// Solicitud para actualizar título, descripción y prioridad de un caso abierto o en proceso.
/// </summary>
public record ActualizarCasoRequest(string Titulo, string Descripcion, string Prioridad);

/// <summary>
/// Solicitud para ejecutar una transición controlada de estado sobre un caso.
/// </summary>
public record CambiarEstadoRequest(string NuevoEstado);

/// <summary>
/// Solicitud para registrar una respuesta humana publicada en un caso.
/// </summary>
public record CrearRespuestaRequest(string Texto);

/// <summary>
/// Solicitud del analista para aprobar y/o editar una sugerencia generada por IA.
/// </summary>
public record AprobarSugerenciaRequest(string TextoFinal);

/// <summary>
/// Vista resumida de un caso almacenado en la papelera de reciclaje.
/// </summary>
public record CasoEliminadoDto(
    Guid Id,
    string Titulo,
    string Prioridad,
    string Estado,
    DateTime FechaCreacion,
    DateTime? FechaEliminacion,
    string ClienteNombre);

/// <summary>
/// Resultado de restaurar los casos de demostración predeterminados en la base de datos.
/// </summary>
public record RestaurarDemoResponse(int CasosCreados, string Mensaje);

/// <summary>
/// Solicitud para escalar o ajustar la prioridad de un caso con justificación obligatoria.
/// </summary>
public record CambiarPrioridadRequest(
    [Required(ErrorMessage = "La nueva prioridad es obligatoria.")]
    PrioridadCaso NuevaPrioridad,
    [Required(ErrorMessage = "La justificación es obligatoria.")]
    [MinLength(10, ErrorMessage = "La justificación debe tener al menos 10 caracteres.")]
    string Justificacion);

/// <summary>
/// Resultado intermedio de la consulta RAG antes de transmitir por SSE y persistir como borrador.
/// </summary>
public record SugerenciaIaPreparada(
    string Respuesta,
    List<FuenteCitadaDto> Fuentes,
    int TokensUsados);

/// <summary>
/// Indicadores ejecutivos operativos y de consumo FinOps de IA para MesaAyuda IA.
/// </summary>
public record MetricasMesaAyudaDto(
    int TotalCasos,
    int CasosAbiertos,
    int CasosEnProceso,
    int CasosResueltos,
    int CasosCerrados,
    int CasosCriticosActivos,
    int SugerenciasIaGeneradas,
    int SugerenciasIaAprobadas,
    double TasaAdopcionIaPorcentaje,
    int TokensTotalesConsumidos,
    double? PromedioCsat,
    int TotalEncuestasCsat);

/// <summary>
/// Solicitud de deflexión preventiva antes de crear un ticket en base de datos.
/// </summary>
public record DeflexionRequest(string Titulo, string Descripcion);

/// <summary>
/// Respuesta de deflexión preventiva con guía oficial resumida y hasta 2 fuentes citadas.
/// </summary>
public record DeflexionResponse(
    bool TieneSugerencia,
    string RespuestaSugerida,
    List<FuenteCitadaDto> Fuentes);

/// <summary>
/// Encuesta de satisfacción CSAT enviada por el cliente al resolver o cerrar su caso.
/// </summary>
public record RegistrarCsatRequest(
    [Range(1, 5, ErrorMessage = "La calificación CSAT debe estar entre 1 y 5.")]
    int Calificacion,
    [MaxLength(300, ErrorMessage = "El comentario CSAT no puede superar los 300 caracteres.")]
    string? Comentario = null);
