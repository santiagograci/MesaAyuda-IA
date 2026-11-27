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
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
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
