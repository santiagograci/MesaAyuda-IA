namespace MesaAyuda.Domain.Enums;

/// <summary>
/// Roles autorizados para autenticación y control de acceso en la plataforma.
/// </summary>
public enum RolUsuario
{
    /// <summary>Usuario empresarial que reporta y consulta sus propios casos.</summary>
    Cliente = 1,

    /// <summary>Especialista de soporte autorizado para gestionar, responder y cerrar casos.</summary>
    Analista = 2
}
