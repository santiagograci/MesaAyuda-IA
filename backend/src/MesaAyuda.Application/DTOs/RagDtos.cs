namespace MesaAyuda.Application.DTOs;

/// <summary>
/// Solicitud enviada al microservicio RAG de Python para obtener una sugerencia anclada.
/// </summary>
public record RagPreguntaRequest(string Pregunta, int TopK = 4);

/// <summary>
/// Respuesta estructurada devuelta por el microservicio RAG (o su fallback de resiliencia).
/// </summary>
public record RagPreguntaResponse(
    string Respuesta,
    List<FuenteCitadaDto> Fuentes,
    int TokensUsados,
    bool Anclado,
    string ProveedorLlm);
