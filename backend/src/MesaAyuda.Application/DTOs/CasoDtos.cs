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
    bool TieneSugerenciaPendiente);

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
    List<RespuestaDto> Respuestas);

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
