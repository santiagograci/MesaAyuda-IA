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
}
