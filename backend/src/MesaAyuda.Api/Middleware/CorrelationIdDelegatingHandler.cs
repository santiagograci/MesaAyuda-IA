namespace MesaAyuda.Api.Middleware;

/// <summary>
/// DelegatingHandler que propaga automáticamente el encabezado <c>X-Correlation-ID</c>
/// de la solicitud entrante hacia todas las llamadas salientes al microservicio RAG.
/// </summary>
public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Inicializa el handler con acceso al contexto HTTP actual.
    /// </summary>
    public CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Adjunta <c>X-Correlation-ID</c> en la petición HTTP saliente antes de enviarla al servicio RAG.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var correlationId = httpContext?.Items[CorrelationIdMiddleware.HeaderName] as string
                ?? CorrelationIdMiddleware.ResolveCorrelationId(
                    httpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault());

            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
