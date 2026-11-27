using FluentValidation;
using MesaAyuda.Application.DTOs;
using MesaAyuda.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MesaAyuda.Api.Controllers;

/// <summary>
/// Controlador responsable de la autenticación JWT y consulta de usuarios de demostración.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<LoginRequest> _loginValidator;

    /// <summary>
    /// Inicializa el controlador de autenticación.
    /// </summary>
    public AuthController(IAuthService authService, IValidator<LoginRequest> loginValidator)
    {
        _authService = authService;
        _loginValidator = loginValidator;
    }

    /// <summary>
    /// Autentica un usuario por correo electrónico y contraseña devolviendo un token JWT firmado.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new
            {
                mensaje = "Datos de inicio de sesión inválidos.",
                errores = validationResult.Errors.Select(e => e.ErrorMessage)
            });
        }

        var response = await _authService.LoginAsync(request, cancellationToken);
        if (response is null)
        {
            return Unauthorized(new { mensaje = "Credenciales inválidas. Verifique correo y contraseña." });
        }

        return Ok(response);
    }

    /// <summary>
    /// Retorna la lista de usuarios de demostración disponibles para pruebas rápidas del portal.
    /// </summary>
    [HttpGet("demo-users")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<DemoUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DemoUserDto>>> GetDemoUsers(CancellationToken cancellationToken)
    {
        var users = await _authService.GetDemoUsersAsync(cancellationToken);
        return Ok(users);
    }
}
