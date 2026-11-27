using Microsoft.Extensions.Configuration;

namespace MesaAyuda.Infrastructure.Security;

/// <summary>
/// Configuración tipada para la emisión y validación de tokens JWT.
/// </summary>
public class JwtSettings
{
    /// <summary>Clave secreta simétrica HMAC-SHA256 para firmar tokens.</summary>
    public string Secret { get; init; } = "SuperSecretKeyForMesaAyudaIA2026OfimaTechLead!";

    /// <summary>Emisor (<c>iss</c>) esperado del token.</summary>
    public string Issuer { get; init; } = "MesaAyuda.Api";

    /// <summary>Audiencia (<c>aud</c>) esperada del token.</summary>
    public string Audience { get; init; } = "MesaAyuda.Client";

    /// <summary>Duración de vigencia del token en horas.</summary>
    public int ExpirationHours { get; init; } = 8;

    /// <summary>
    /// Construye la configuración JWT priorizando variables de entorno estándar sobre <c>appsettings</c>.
    /// </summary>
    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var secret = configuration["JWT_SECRET"]
            ?? configuration["Jwt:Secret"]
            ?? "SuperSecretKeyForMesaAyudaIA2026OfimaTechLead!";

        var issuer = configuration["JWT_ISSUER"]
            ?? configuration["Jwt:Issuer"]
            ?? "MesaAyuda.Api";

        var audience = configuration["JWT_AUDIENCE"]
            ?? configuration["Jwt:Audience"]
            ?? "MesaAyuda.Client";

        return new JwtSettings
        {
            Secret = secret,
            Issuer = issuer,
            Audience = audience,
            ExpirationHours = 8
        };
    }
}
