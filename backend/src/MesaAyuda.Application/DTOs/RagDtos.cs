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

/// <summary>
/// Resumen de un manual indexado en la base vectorial RAG y su cantidad de fragmentos.
/// </summary>
public record DocumentoIndexadoDto(string Documento, int Fragmentos);

/// <summary>
/// Inventario completo de manuales indexados devuelto por GET /documentos del servicio RAG.
/// </summary>
public record DocumentosRagResponse(
    List<DocumentoIndexadoDto> Documentos,
    int TotalDocumentos,
    int TotalFragmentos);

/// <summary>
/// Resultado de ingestar e indexar un manual individual en el servicio RAG.
/// </summary>
public record IngestaArchivoResponse(
    string Archivo,
    int FragmentosArchivo,
    int TotalDocumentos,
    int TotalFragmentos,
    string Estado);
