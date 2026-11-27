using System.Net.Http.Json;
using System.Text.Json;
using MesaAyuda.Application.DTOs;
using MesaAyuda.Application.Interfaces;
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

    private static RagPreguntaResponse CreateFallbackResponse() =>
        new(
            Respuesta: FallbackMessage,
            Fuentes: new List<FuenteCitadaDto>(),
            TokensUsados: 0,
            Anclado: false,
            ProveedorLlm: "fallback-resilience");
}
