using MesaAyuda.Application.DTOs;

namespace MesaAyuda.Application.Interfaces;

/// <summary>
/// Caso de uso de autenticación y consulta de usuarios de demostración.
/// </summary>
public interface IAuthService
{
    /// <summary>Valida credenciales y retorna el token JWT o <c>null</c> si son inválidas.</summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Retorna la lista de cuentas de demostración registradas sin exponer credenciales.</summary>
    Task<IReadOnlyList<DemoUserDto>> GetDemoUsersAsync(CancellationToken cancellationToken = default);
}
