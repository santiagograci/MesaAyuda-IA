namespace MesaAyuda.Application.DTOs;

/// <summary>
/// Solicitud de autenticación mediante correo electrónico y contraseña.
/// </summary>
public record LoginRequest(string Email, string Password);

/// <summary>
/// Respuesta emitida tras una autenticación exitosa con el token JWT y perfil del usuario.
/// </summary>
public record LoginResponse(
    string Token,
    Guid UserId,
    string Nombre,
    string Email,
    string Rol);

/// <summary>
/// Información pública de un usuario de demostración para inicio rápido en el portal.
/// </summary>
public record DemoUserDto(
    string Nombre,
    string Email,
    string Rol);
