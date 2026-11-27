using MesaAyuda.Domain.Common;
using MesaAyuda.Domain.Exceptions;

namespace MesaAyuda.Domain.Entities;

/// <summary>
/// Representa una respuesta humana o una sugerencia generada por IA asociada a un caso.
/// </summary>
public class Respuesta
{
    /// <summary>Identificador único de la respuesta.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador del caso al que pertenece la respuesta.</summary>
    public Guid CasoId { get; private set; }

    /// <summary>Contenido textual de la respuesta o borrador sugerido.</summary>
    public string Texto { get; private set; } = string.Empty;

    /// <summary>Indica si la respuesta fue originada por el servicio RAG de IA.</summary>
    public bool EsGeneradaPorIA { get; private set; }

    /// <summary>Indica si un analista aprobó la respuesta para su publicación oficial.</summary>
    public bool AprobadaPorAnalista { get; private set; }

    /// <summary>Nombre del autor o analista que publicó o aprobó la respuesta.</summary>
    public string AutorNombre { get; private set; } = string.Empty;

    /// <summary>Arreglo JSON serializado con las fuentes del manual ERP citadas por la IA.</summary>
    public string FuentesCitadasJson { get; private set; } = "[]";

    /// <summary>Cantidad de tokens consumidos al generar la respuesta de IA (FinOps).</summary>
    public int TokensUsados { get; private set; }

    /// <summary>Fecha de registro de la respuesta en formato UTC.</summary>
    public DateTime FechaCreacion { get; private set; }

    /// <summary>Captura de pantalla opcional adjunta en formato Data URL Base64.</summary>
    public string? ImagenAdjuntaBase64 { get; private set; }

    private Respuesta()
    {
    }

    /// <summary>
    /// Crea una nueva instancia de respuesta vinculada a un caso existente.
    /// </summary>
    public Respuesta(
        Guid casoId,
        string texto,
        bool esGeneradaPorIA,
        bool aprobadaPorAnalista,
        string autorNombre,
        string fuentesCitadasJson = "[]",
        int tokensUsados = 0,
        string? imagenAdjuntaBase64 = null)
    {
        if (casoId == Guid.Empty)
            throw new DomainException("El identificador del caso es obligatorio para registrar una respuesta.");
        if (string.IsNullOrWhiteSpace(texto))
            throw new DomainException("El texto de la respuesta no puede estar vacío.");
        if (string.IsNullOrWhiteSpace(autorNombre))
            throw new DomainException("El nombre del autor de la respuesta es obligatorio.");

        Id = Guid.NewGuid();
        CasoId = casoId;
        Texto = texto.Trim();
        EsGeneradaPorIA = esGeneradaPorIA;
        AprobadaPorAnalista = aprobadaPorAnalista;
        AutorNombre = autorNombre.Trim();
        FuentesCitadasJson = string.IsNullOrWhiteSpace(fuentesCitadasJson) ? "[]" : fuentesCitadasJson.Trim();
        TokensUsados = Math.Max(0, tokensUsados);
        FechaCreacion = DateTime.UtcNow;
        ImagenAdjuntaBase64 = ImageAttachmentPolicy.NormalizeAndValidate(imagenAdjuntaBase64);
    }

    /// <summary>
    /// Aprueba y opcionalmente edita una respuesta sugerida por IA registrando al analista responsable.
    /// </summary>
    public void AprobarYEditar(string textoFinal, string nombreAnalista)
    {
        if (string.IsNullOrWhiteSpace(textoFinal))
            throw new DomainException("El texto final de la respuesta aprobada no puede estar vacío.");
        if (string.IsNullOrWhiteSpace(nombreAnalista))
            throw new DomainException("El nombre del analista que aprueba es obligatorio.");

        Texto = textoFinal.Trim();
        AprobadaPorAnalista = true;
        AutorNombre = nombreAnalista.Trim();
    }
}
