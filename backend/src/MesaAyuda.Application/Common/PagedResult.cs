namespace MesaAyuda.Application.Common;

/// <summary>
/// Contenedor genérico para resultados paginados de consultas empresariales.
/// </summary>
public class PagedResult<T>
{
    /// <summary>Elementos correspondientes a la página actual.</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>Total global de registros que cumplen el filtro aplicado.</summary>
    public int TotalCount { get; init; }

    /// <summary>Número de página actual (base 1).</summary>
    public int Page { get; init; }

    /// <summary>Cantidad máxima de elementos por página.</summary>
    public int PageSize { get; init; }

    /// <summary>Número total de páginas disponibles según <see cref="TotalCount"/> y <see cref="PageSize"/>.</summary>
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
