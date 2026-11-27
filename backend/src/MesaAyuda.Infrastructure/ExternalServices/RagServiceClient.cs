using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MesaAyuda.Application.DTOs;
using MesaAyuda.Application.Interfaces;
using MesaAyuda.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace MesaAyuda.Infrastructure.ExternalServices;

/// <summary>
/// Cliente HTTP tipado y resiliente hacia el microservicio Python FastAPI RAG.
/// </summary>
public class RagServiceClient : IRagServiceClient
{
    private const string FallbackMessage =
        "[Servicio de IA temporalmente no disponible. Por favor, responda manualmente o reintente en unos instantes.]";

    private static readonly JsonSerializerOptions SnakeCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<RagServiceClient> _logger;

    /// <summary>
    /// Inicializa el cliente RAG con el <see cref="HttpClient"/> gestionado por Polly y logger estructurado.
    /// </summary>
    public RagServiceClient(HttpClient httpClient, ILogger<RagServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Consulta el endpoint <c>POST /preguntar</c> aplicando reintentos, timeout y circuit breaker,
    /// devolviendo una respuesta controlada si el servicio de IA no está disponible.
    /// </summary>
    public async Task<RagPreguntaResponse> ConsultarSugerenciaAsync(
        string pregunta,
        CancellationToken cancellationToken = default)
    {
        var payload = new RagPreguntaRequest(pregunta, TopK: 4);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "/preguntar",
                payload,
                SnakeCaseJsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "El servicio RAG respondió con estado HTTP {StatusCode}. Detalle: {Body}",
                    (int)response.StatusCode,
                    body);
                return CreateFallbackResponse();
            }

            var ragResponse = await response.Content.ReadFromJsonAsync<RagPreguntaResponse>(
                SnakeCaseJsonOptions,
                cancellationToken);

            if (ragResponse is null)
            {
                _logger.LogWarning("El servicio RAG devolvió un cuerpo JSON vacío.");
                return CreateFallbackResponse();
            }

            return ragResponse;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "Circuit breaker abierto al consultar el servicio RAG.");
            return CreateFallbackResponse();
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "Timeout por intento excedido al consultar el servicio RAG.");
            return CreateFallbackResponse();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error de red HTTP al comunicarse con el servicio RAG.");
            return CreateFallbackResponse();
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "La solicitud al servicio RAG expiró por cancelación de timeout.");
            return CreateFallbackResponse();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Error deserializando la respuesta del servicio RAG.");
            return CreateFallbackResponse();
        }
    }

    /// <summary>
    /// Consulta el endpoint <c>GET /documentos</c> para obtener el inventario indexado en el RAG.
    /// </summary>
    public async Task<DocumentosRagResponse> ListarDocumentosAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/documentos", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"El servicio RAG devolvió HTTP {(int)response.StatusCode} al listar documentos: {body}");
        }

        return await response.Content.ReadFromJsonAsync<DocumentosRagResponse>(
            SnakeCaseJsonOptions,
            cancellationToken)
            ?? new DocumentosRagResponse(new List<DocumentoIndexadoDto>(), 0, 0);
    }

    /// <summary>
    /// Envía un manual en memoria al endpoint <c>POST /ingesta/archivo</c> mediante multipart/form-data.
    /// </summary>
    public async Task<IngestaArchivoResponse> IngestarArchivoAsync(
        string fileName,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var multipart = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        multipart.Add(fileContent, "file", fileName);

        using var response = await _httpClient.PostAsync("/ingesta/archivo", multipart, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = ExtractFastApiDetail(errorBody) ?? "El archivo no cumple las validaciones del servicio RAG.";
            throw new DomainException(detail);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"El servicio RAG devolvió HTTP {(int)response.StatusCode} al ingestar '{fileName}': {body}");
        }

        return await response.Content.ReadFromJsonAsync<IngestaArchivoResponse>(
            SnakeCaseJsonOptions,
            cancellationToken)
            ?? throw new HttpRequestException("El servicio RAG devolvió una respuesta vacía tras la ingesta.");
    }

    private static string? ExtractFastApiDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detailProp))
                return detailProp.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
        return null;
    }

    private static RagPreguntaResponse CreateFallbackResponse() =>
        new(
            Respuesta: FallbackMessage,
            Fuentes: new List<FuenteCitadaDto>(),
            TokensUsados: 0,
            Anclado: false,
            ProveedorLlm: "fallback-resilience");
}
