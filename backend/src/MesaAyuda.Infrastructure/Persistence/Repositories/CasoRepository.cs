using MesaAyuda.Domain.Entities;
using MesaAyuda.Domain.Enums;
using MesaAyuda.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MesaAyuda.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación EF Core del repositorio de casos con carga de respuestas y paginación filtrada.
/// </summary>
public class CasoRepository : ICasoRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    public CasoRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Recupera un caso por identificador incluyendo cliente y respuestas ordenadas cronológicamente.
    /// </summary>
    public async Task<Caso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Casos
            .Include(c => c.Cliente)
            .Include(c => c.Respuestas.OrderBy(r => r.FechaCreacion))
            .FirstOrDefaultAsync(c => c.Id == id && !c.Eliminado, cancellationToken);
    }

    /// <summary>
    /// Recupera un caso por ID incluyendo casos en papelera para habilitar su restauración.
    /// </summary>
    public async Task<Caso?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Casos
            .Include(c => c.Cliente)
            .Include(c => c.Respuestas.OrderBy(r => r.FechaCreacion))
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    /// <summary>
    /// Lista todos los casos marcados como eliminados ordenados por fecha de eliminación descendente.
    /// </summary>
    public async Task<IReadOnlyList<Caso>> ListarEliminadosAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Casos
            .AsNoTracking()
            .Include(c => c.Cliente)
            .Where(c => c.Eliminado)
            .OrderByDescending(c => c.FechaEliminacion ?? c.FechaCreacion)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Verifica si existe al menos un caso activo con el título indicado.
    /// </summary>
    public async Task<bool> ExistsActiveByTitleAsync(string titulo, CancellationToken cancellationToken = default)
    {
        var normalized = titulo.Trim().ToLowerInvariant();
        return await _dbContext.Casos
            .AsNoTracking()
            .AnyAsync(c => !c.Eliminado && c.Titulo.ToLower() == normalized, cancellationToken);
    }

    /// <summary>
    /// Consulta casos paginados aplicando filtros opcionales compatibles con PostgreSQL e InMemory.
    /// </summary>
    public async Task<(IReadOnlyList<Caso> Items, int TotalCount)> GetPagedAsync(
        Guid? clienteId,
        EstadoCaso? estado,
        PrioridadCaso? prioridad,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Casos
            .AsNoTracking()
            .Include(c => c.Cliente)
            .Include(c => c.Respuestas)
            .Where(c => !c.Eliminado)
            .AsQueryable();

        if (clienteId.HasValue)
            query = query.Where(c => c.ClienteId == clienteId.Value);

        if (estado.HasValue)
            query = query.Where(c => c.Estado == estado.Value);

        if (prioridad.HasValue)
            query = query.Where(c => c.Prioridad == prioridad.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.Titulo.ToLower().Contains(term) ||
                c.Descripcion.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderByDescending(c => c.FechaCreacion)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Agrega un nuevo caso al contexto para su persistencia en la unidad de trabajo.
    /// </summary>
    public async Task AddAsync(Caso caso, CancellationToken cancellationToken = default)
    {
        await _dbContext.Casos.AddAsync(caso, cancellationToken);
    }

    /// <summary>
    /// Lista todos los casos activos incluyendo sus respuestas para agregación de métricas KPI.
    /// </summary>
    public async Task<IReadOnlyList<Caso>> ListarActivosConRespuestasAsync(
        Guid? clienteId,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Casos
            .AsNoTracking()
            .Include(c => c.Respuestas)
            .Where(c => !c.Eliminado)
            .AsQueryable();

        if (clienteId.HasValue)
            query = query.Where(c => c.ClienteId == clienteId.Value);

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Registra explícitamente una respuesta hija nueva para evitar conflictos de concurrencia en EF Core.
    /// </summary>
    public async Task AddRespuestaAsync(Respuesta respuesta, CancellationToken cancellationToken = default)
    {
        await _dbContext.Respuestas.AddAsync(respuesta, cancellationToken);
    }

    /// <summary>
    /// Marca un caso existente para eliminación física en la base de datos.
    /// </summary>
    public void Remove(Caso caso)
    {
        _dbContext.Casos.Remove(caso);
    }
}
