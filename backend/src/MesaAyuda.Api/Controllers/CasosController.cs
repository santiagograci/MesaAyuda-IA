using System.Security.Claims;
using MesaAyuda.Application.Common;
using MesaAyuda.Application.DTOs;
using MesaAyuda.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MesaAyuda.Api.Controllers;

/// <summary>
/// Controlador protegido con JWT para la gestión de casos, máquina de estados,
/// respuestas humanas y sugerencias asistidas por IA (RAG).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CasosController : ControllerBase
{
    private readonly ICasoService _casoService;

    /// <summary>
    /// Inicializa el controlador de casos.
    /// </summary>
    public CasosController(ICasoService casoService)
    {
        _casoService = casoService;
    }

    /// <summary>
    /// Lista casos paginados con filtro opcional por estado (los clientes solo ven sus propios casos).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CasoResumenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<CasoResumenDto>>> Listar(
        [FromQuery] string? estado = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (usuarioId, rol, _) = GetCurrentUser();
        var resultado = await _casoService.ListarCasosAsync(usuarioId, rol, estado, page, pageSize, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>
    /// Obtiene el detalle completo de un caso incluyendo su historial de respuestas y fuentes citadas.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CasoDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CasoDetalleDto>> ObtenerPorId(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var (usuarioId, rol, _) = GetCurrentUser();
        var caso = await _casoService.ObtenerPorIdAsync(id, usuarioId, rol, cancellationToken);
        return Ok(caso);
    }

    /// <summary>
    /// Crea un nuevo caso de soporte en estado inicial Abierto.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CasoDetalleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CasoDetalleDto>> Crear(
        [FromBody] CrearCasoRequest request,
        CancellationToken cancellationToken = default)
    {
        var (usuarioId, _, _) = GetCurrentUser();
        var creado = await _casoService.CrearCasoAsync(request, usuarioId, cancellationToken);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = creado.Id }, creado);
    }

    /// <summary>
    /// Actualiza título, descripción y prioridad de un caso que no esté Cerrado.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CasoDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CasoDetalleDto>> Actualizar(
        Guid id,
        [FromBody] ActualizarCasoRequest request,
        CancellationToken cancellationToken = default)
    {
        var (usuarioId, rol, _) = GetCurrentUser();
        var actualizado = await _casoService.ActualizarCasoAsync(id, request, usuarioId, rol, cancellationToken);
        return Ok(actualizado);
    }

    /// <summary>
    /// Elimina un caso de soporte (acción exclusiva del rol Analista).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Analista")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var (_, rol, _) = GetCurrentUser();
        await _casoService.EliminarCasoAsync(id, rol, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Ejecuta la máquina de estados del dominio sobre un caso validando sus invariantes.
    /// </summary>
    [HttpPatch("{id:guid}/estado")]
    [ProducesResponseType(typeof(CasoDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CasoDetalleDto>> CambiarEstado(
        Guid id,
        [FromBody] CambiarEstadoRequest request,
        CancellationToken cancellationToken = default)
    {
        var actualizado = await _casoService.CambiarEstadoAsync(id, request.NuevoEstado, cancellationToken);
        return Ok(actualizado);
    }

    /// <summary>
    /// Registra una respuesta humana publicada en el historial del caso.
    /// </summary>
    [HttpPost("{id:guid}/respuestas")]
    [ProducesResponseType(typeof(RespuestaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RespuestaDto>> AgregarRespuesta(
        Guid id,
        [FromBody] CrearRespuestaRequest request,
        CancellationToken cancellationToken = default)
    {
        var (usuarioId, rol, nombreUsuario) = GetCurrentUser();
        var respuesta = await _casoService.AgregarRespuestaHumanaAsync(
            id,
            request,
            usuarioId,
            nombreUsuario,
            rol,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, respuesta);
    }

    /// <summary>
    /// Consulta al microservicio RAG y registra un borrador de respuesta sugerido por IA con citas.
    /// </summary>
    [HttpPost("{id:guid}/sugerencia")]
    [ProducesResponseType(typeof(RespuestaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RespuestaDto>> GenerarSugerenciaIA(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var (usuarioId, rol, _) = GetCurrentUser();
        var sugerencia = await _casoService.GenerarSugerenciaIAAsync(id, usuarioId, rol, cancellationToken);
        return Ok(sugerencia);
    }

    /// <summary>
    /// Aprueba y opcionalmente edita una sugerencia generada por IA para publicarla al cliente.
    /// </summary>
    [HttpPut("{id:guid}/respuestas/{respuestaId:guid}/aprobar")]
    [Authorize(Roles = "Analista")]
    [ProducesResponseType(typeof(RespuestaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RespuestaDto>> AprobarSugerenciaIA(
        Guid id,
        Guid respuestaId,
        [FromBody] AprobarSugerenciaRequest request,
        CancellationToken cancellationToken = default)
    {
        var (_, _, nombreAnalista) = GetCurrentUser();
        var aprobada = await _casoService.AprobarSugerenciaIAAsync(
            id,
            respuestaId,
            request,
            nombreAnalista,
            cancellationToken);
        return Ok(aprobada);
    }

    private (Guid UsuarioId, string Rol, string Nombre) GetCurrentUser()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(idClaim, out var usuarioId))
            throw new UnauthorizedAccessException("Token JWT inválido: falta el identificador de usuario.");

        var rol = User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role")
            ?? string.Empty;

        var nombre = User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("name")
            ?? "Usuario MesaAyuda";

        return (usuarioId, rol, nombre);
    }
}
