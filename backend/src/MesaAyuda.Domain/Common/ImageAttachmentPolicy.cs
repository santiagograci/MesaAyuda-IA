using System.Text.RegularExpressions;
using MesaAyuda.Domain.Exceptions;

namespace MesaAyuda.Domain.Common;

/// <summary>
/// Política de validación de capturas o evidencias visuales adjuntas en formato Data URL (PNG/JPG/WebP, máx. 2 MB).
/// </summary>
public static partial class ImageAttachmentPolicy
{
    /// <summary>Tamaño máximo decodificado permitido por imagen adjunta (2 MB).</summary>
    public const int MaxImageBytes = 2 * 1024 * 1024;

    private const int MaxBase64Length = 2_900_000;
    private const string ErrorMessage = "La imagen adjunta debe ser PNG, JPG o WebP válida y no superar los 2 MB.";

    [GeneratedRegex(@"^data:image/(png|jpeg|jpg|webp);base64,", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrefixRegex();

    /// <summary>
    /// Normaliza y valida una imagen en formato Data URL Base64; retorna <c>null</c> si es nula o vacía.
    /// </summary>
    public static string? NormalizeAndValidate(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
            return null;

        var trimmed = dataUrl.Trim();
        var match = PrefixRegex().Match(trimmed);
        if (!match.Success)
            throw new DomainException(ErrorMessage);

        var payload = trimmed[match.Length..];
        if (payload.Length == 0 || payload.Length > MaxBase64Length)
            throw new DomainException(ErrorMessage);

        try
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length == 0 || bytes.Length > MaxImageBytes)
                throw new DomainException(ErrorMessage);
        }
        catch (FormatException)
        {
            throw new DomainException(ErrorMessage);
        }

        return trimmed;
    }

    /// <summary>
    /// Retorna <c>true</c> si el adjunto es nulo/vacío o cumple el formato y tamaño máximo permitidos.
    /// </summary>
    public static bool IsValidOrEmpty(string? dataUrl)
    {
        try
        {
            NormalizeAndValidate(dataUrl);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
