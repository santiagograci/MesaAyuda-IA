using MesaAyuda.Application.DTOs;

namespace MesaAyuda.Application.Interfaces;

/// <summary>
/// Cliente HTTP resiliente para consultar sugerencias técnicas al microservicio RAG.
/// </summary>
public interface IRagServiceClient
{
    /// <summary>
    /// Envía la consulta del caso al servicio RAG y retorna una respuesta controlada ante fallos.
    /// </summary>
    Task<RagPreguntaResponse> ConsultarSugerenciaAsync(
        string pregunta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el inventario actual de documentos indexados en el microservicio RAG.
    /// </summary>
    Task<DocumentosRagResponse> ListarDocumentosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reenvía un manual (.md, .txt o .pdf) al microservicio RAG para su ingesta e indexación inmediata.
    /// </summary>
    Task<IngestaArchivoResponse> IngestarArchivoAsync(
        string fileName,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);
}
