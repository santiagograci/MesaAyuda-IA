using MesaAyuda.Application.DTOs;
using MesaAyuda.Application.Interfaces;
using MesaAyuda.Domain.Interfaces;

namespace MesaAyuda.Application.Services;

/// <summary>
/// Implementa la autenticación de usuarios y la emisión de tokens JWT por rol.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    /// <summary>
    /// Inicializa el servicio de autenticación con sus dependencias de dominio y seguridad.
    /// </summary>
    public AuthService(
        IUsuarioRepository usuarioRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _usuarioRepository = usuarioRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    /// <summary>
    /// Autentica al usuario por correo y contraseña, devolviendo su token JWT con claims de rol.
    /// </summary>
    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return null;

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var usuario = await _usuarioRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (usuario is null || !_passwordHasher.Verify(request.Password, usuario.PasswordHash))
            return null;

        var token = _jwtTokenGenerator.GenerateToken(usuario);
        return new LoginResponse(
            Token: token,
            UserId: usuario.Id,
            Nombre: usuario.Nombre,
            Email: usuario.Email,
            Rol: usuario.Rol.ToString());
    }

    /// <summary>
    /// Obtiene los usuarios de demostración disponibles para facilitar pruebas en el portal web.
    /// </summary>
    public async Task<IReadOnlyList<DemoUserDto>> GetDemoUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var usuarios = await _usuarioRepository.GetAllAsync(cancellationToken);
        return usuarios
            .Select(u => new DemoUserDto(u.Nombre, u.Email, u.Rol.ToString()))
            .ToList();
    }
}
