using System.Text.RegularExpressions;

namespace MesaAyuda.Api.Middleware;

/// <summary>
/// Middleware ligero que lee o genera el identificador <c>X-Correlation-ID</c>,
/// lo propaga en la respuesta HTTP y abre un ámbito de logging estructurado.
/// </summary>
public partial class CorrelationIdMiddleware
{
    /// <summary>Nombre estándar de la cabecera de correlación distribuida.</summary>
    public const string HeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    [GeneratedRegex(@"^[a-zA-Z0-9._-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeCorrelationIdRegex();

    /// <summary>
    /// Inicializa el middleware de correlación distribuida.
    /// </summary>
    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Resuelve <c>X-Correlation-ID</c>, lo registra en la respuesta y abre el scope de <see cref="ILogger"/>.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers[HeaderName].FirstOrDefault());

        context.Items[HeaderName] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }

    /// <summary>
    /// Valida un identificador entrante seguro o genera un UUID nuevo si está ausente o es inválido.
    /// </summary>
    public static string ResolveCorrelationId(string? incoming)
    {
        var candidate = incoming?.Trim();
        if (!string.IsNullOrWhiteSpace(candidate) && SafeCorrelationIdRegex().IsMatch(candidate))
            return candidate;

        return Guid.NewGuid().ToString();
    }
}
