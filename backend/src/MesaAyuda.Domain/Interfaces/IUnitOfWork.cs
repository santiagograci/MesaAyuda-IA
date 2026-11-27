namespace MesaAyuda.Domain.Interfaces;

/// <summary>
/// Contrato de Unidad de Trabajo para confirmar transacciones atómicas sobre la base de datos.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persiste de forma atómica todos los cambios rastreados en el contexto.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
