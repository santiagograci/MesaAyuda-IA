using MesaAyuda.Domain.Interfaces;

namespace MesaAyuda.Infrastructure.Persistence;

/// <summary>
/// Implementación de Unidad de Trabajo respaldada por <see cref="AppDbContext"/>.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// Inicializa la unidad de trabajo.
    /// </summary>
    public UnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Confirma los cambios pendientes en la base de datos.
    /// </summary>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
