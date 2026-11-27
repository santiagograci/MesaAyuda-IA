using MesaAyuda.Domain.Entities;

namespace MesaAyuda.Application.Interfaces;

/// <summary>
/// Servicio encargado de generar tokens JWT firmados para usuarios autenticados.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>Genera un JWT con los claims de identidad y rol del usuario.</summary>
    string GenerateToken(Usuario usuario);
}
