using MesaAyuda.Application.DTOs;
using MesaAyuda.Application.Interfaces;
using MesaAyuda.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace MesaAyuda.Api.Controllers;

/// <summary>
/// Controlador exclusivo para Analistas que actúa como puerta de enlace segura hacia la base
/// de conocimiento documental del microservicio RAG.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Analista")]
public class DocumentosController : ControllerBase
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".txt",
        ".pdf"
    };

    private readonly IRagServiceClient _ragServiceClient;

    /// <summary>
    /// Inicializa el controlador de gestión documental RAG.
    /// </summary>
    public DocumentosController(IRagServiceClient ragServiceClient)
    {
        _ragServiceClient = ragServiceClient;
    }

    /// <summary>
    /// Obtiene la lista de manuales ERP indexados en ChromaDB y su conteo de fragmentos.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DocumentosRagResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DocumentosRagResponse>> Listar(CancellationToken cancellationToken = default)
    {
        try
        {
            var resultado = await _ragServiceClient.ListarDocumentosAsync(cancellationToken);
            return Ok(resultado);
        }
        catch (Exception ex) when (IsRagUnavailableException(ex, cancellationToken))
        {
            return Problem(
                title: "Servicio RAG no disponible",
                detail: "No fue posible consultar el inventario de documentos en el servicio de IA.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// Valida e ingesta un nuevo manual (.md, .txt o .pdf, máx. 5 MB) hacia el servicio RAG.
    /// </summary>
    [HttpPost("ingestar")]
    [ProducesResponseType(typeof(IngestaArchivoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IngestaArchivoResponse>> Ingestar(
        IFormFile? archivo,
        CancellationToken cancellationToken = default)
    {
        if (archivo is null || archivo.Length == 0)
            throw new DomainException("Debe seleccionar un archivo no vacío (.md, .txt o .pdf).");

        if (archivo.Length > MaxUploadBytes)
            throw new DomainException("El archivo supera el tamaño máximo permitido de 5 MB.");

        var ext = Path.GetExtension(archivo.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            throw new DomainException("Formato de archivo no válido. Solo se admiten archivos .md, .txt o .pdf.");

        await using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        try
        {
            var resultado = await _ragServiceClient.IngestarArchivoAsync(
                archivo.FileName,
                bytes,
                archivo.ContentType,
                cancellationToken);
            return Ok(resultado);
        }
        catch (Exception ex) when (IsRagUnavailableException(ex, cancellationToken))
        {
            return Problem(
                title: "Servicio RAG no disponible",
                detail: "No fue posible enviar el manual al servicio de IA. Reintente en unos instantes.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool IsRagUnavailableException(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException
        || ex is BrokenCircuitException
        || ex is TimeoutRejectedException
        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);
}
