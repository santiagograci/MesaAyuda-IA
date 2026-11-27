namespace MesaAyuda.Domain.Enums;

/// <summary>
/// Nivel de prioridad operativa asignado a un caso de mesa de ayuda.
/// </summary>
public enum PrioridadCaso
{
    /// <summary>Consulta general o requerimiento sin impacto operativo inmediato.</summary>
    Baja = 1,

    /// <summary>Incidente moderado con solución alternativa disponible.</summary>
    Media = 2,

    /// <summary>Afectación significativa sobre un proceso operativo del ERP.</summary>
    Alta = 3,

    /// <summary>Interrupción crítica de facturación, nómina, cierre o seguridad.</summary>
    Critica = 4
}
