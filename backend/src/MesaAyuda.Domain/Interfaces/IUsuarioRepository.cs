using MesaAyuda.Domain.Entities;

namespace MesaAyuda.Domain.Interfaces;

/// <summary>
/// Contrato de repositorio para la consulta de usuarios autenticables.
/// </summary>
public interface IUsuarioRepository
{
    /// <summary>Busca un usuario por su correo electrónico normalizado.</summary>
    Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Obtiene un usuario por su identificador único.</summary>
    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Obtiene todos los usuarios registrados ordenados por rol y nombre.</summary>
    Task<IReadOnlyList<Usuario>> GetAllAsync(CancellationToken cancellationToken = default);
}
