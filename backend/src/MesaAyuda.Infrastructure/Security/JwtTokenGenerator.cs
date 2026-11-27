using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MesaAyuda.Application.Interfaces;
using MesaAyuda.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace MesaAyuda.Infrastructure.Security;

/// <summary>
/// Generador de tokens JWT firmados con HMAC-SHA256 y expiración de 8 horas.
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _jwtSettings;

    /// <summary>
    /// Inicializa el generador con la configuración JWT activa.
    /// </summary>
    public JwtTokenGenerator(JwtSettings jwtSettings)
    {
        _jwtSettings = jwtSettings;
    }

    /// <summary>
    /// Genera un token JWT con los claims estándar (<c>sub</c>, <c>email</c>, <c>name</c>, <c>role</c>).
    /// </summary>
    public string GenerateToken(Usuario usuario)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(JwtRegisteredClaimNames.Name, usuario.Nombre),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Nombre),
            new("role", usuario.Rol.ToString()),
            new(ClaimTypes.Role, usuario.Rol.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_jwtSettings.ExpirationHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
