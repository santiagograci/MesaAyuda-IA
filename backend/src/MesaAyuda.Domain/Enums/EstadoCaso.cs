namespace MesaAyuda.Domain.Enums;

/// <summary>
/// Representa los estados posibles dentro del ciclo de vida de un caso de soporte.
/// </summary>
public enum EstadoCaso
{
    /// <summary>Caso recién registrado sin respuesta publicada.</summary>
    Abierto = 1,

    /// <summary>Caso en atención activa por un analista de soporte.</summary>
    EnProceso = 2,

    /// <summary>Caso con solución propuesta pendiente de confirmación o cierre.</summary>
    Resuelto = 3,

    /// <summary>Caso cerrado de forma definitiva; no admite modificaciones.</summary>
    Cerrado = 4
}
