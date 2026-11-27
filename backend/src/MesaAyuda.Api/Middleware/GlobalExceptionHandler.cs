using FluentValidation;
using MesaAyuda.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MesaAyuda.Api.Middleware;

/// <summary>
/// Manejador global de excepciones que transforma errores de dominio, validación,
/// autorización y fallos inesperados en respuestas estándar <see cref="ProblemDetails"/> RFC 7807.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Inicializa el manejador global de excepciones.
    /// </summary>
    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Intercepta excepciones no capturadas en el pipeline HTTP y escribe un <see cref="ProblemDetails"/> apropiado.
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

            var validationProblem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error de validación",
                Detail = "Uno o más campos de la solicitud presentan errores de validación.",
                Instance = httpContext.Request.Path
            };

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
            return true;
        }

        var (statusCode, title, detail) = exception switch
        {
            DomainException ex => (
                StatusCodes.Status400BadRequest,
                "Regla de negocio incumplida",
                ex.Message),
            KeyNotFoundException ex => (
                StatusCodes.Status404NotFound,
                "Recurso no encontrado",
                ex.Message),
            UnauthorizedAccessException ex => (
                StatusCodes.Status403Forbidden,
                "Acceso denegado",
                ex.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Error interno del servidor",
                _environment.IsDevelopment()
                    ? exception.Message
                    : "Ocurrió un error inesperado al procesar la solicitud.")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Excepción no controlada en {Path}", httpContext.Request.Path);
        else
            _logger.LogWarning("Error controlado ({StatusCode}) en {Path}: {Message}", statusCode, httpContext.Request.Path, exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
