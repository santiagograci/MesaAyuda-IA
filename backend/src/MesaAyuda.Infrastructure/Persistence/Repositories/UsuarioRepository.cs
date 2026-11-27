using MesaAyuda.Domain.Entities;
using MesaAyuda.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MesaAyuda.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación EF Core del repositorio de usuarios autenticables.
/// </summary>
public class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// Inicializa el repositorio de usuarios.
    /// </summary>
    public UsuarioRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Busca un usuario por correo electrónico normalizado.
    /// </summary>
    public async Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await _dbContext.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    /// <summary>
    /// Obtiene un usuario por su identificador único.
    /// </summary>
    public async Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    /// <summary>
    /// Lista todos los usuarios ordenados por rol y nombre.
    /// </summary>
    public async Task<IReadOnlyList<Usuario>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.Rol)
            .ThenBy(u => u.Nombre)
            .ToListAsync(cancellationToken);
    }
}
