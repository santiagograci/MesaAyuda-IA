using MesaAyuda.Domain.Exceptions;

namespace MesaAyuda.Domain.Entities;

/// <summary>
/// Contador global persistente para indicadores del sistema como deflexiones RAG evitadas.
/// </summary>
public class MetricaSistema
{
    /// <summary>Clave única del contador acumulado de tickets evitados mediante deflexión RAG.</summary>
    public const string ClaveDeflexionesEvitadas = "DEFLEXIONES_EVITADAS";

    /// <summary>Identificador único de la métrica.</summary>
    public string Clave { get; private set; } = string.Empty;

    /// <summary>Valor numérico acumulado.</summary>
    public int Valor { get; private set; }

    /// <summary>Fecha UTC de la última actualización del contador.</summary>
    public DateTime FechaActualizacion { get; private set; }

    private MetricaSistema()
    {
    }

    /// <summary>
    /// Crea una nueva entrada de métrica global del sistema.
    /// </summary>
    public MetricaSistema(string clave, int valorInicial = 0)
    {
        if (string.IsNullOrWhiteSpace(clave))
            throw new DomainException("La clave de métrica del sistema es obligatoria.");
        if (valorInicial < 0)
            throw new DomainException("El valor inicial de la métrica no puede ser negativo.");

        Clave = clave.Trim();
        Valor = valorInicial;
        FechaActualizacion = DateTime.UtcNow;
    }

    /// <summary>
    /// Incrementa en 1 unidad el contador acumulado y actualiza la marca de tiempo UTC.
    /// </summary>
    public void Incrementar()
    {
        Valor++;
        FechaActualizacion = DateTime.UtcNow;
    }
}
