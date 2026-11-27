using MesaAyuda.Domain.Entities;
using MesaAyuda.Domain.Enums;

namespace MesaAyuda.Domain.Interfaces;

/// <summary>
/// Contrato de repositorio para la persistencia y consulta paginada de casos de soporte.
/// </summary>
public interface ICasoRepository
{
    /// <summary>Obtiene un caso por su identificador incluyendo sus respuestas y cliente.</summary>
    Task<Caso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Obtiene una página de casos aplicando filtros opcionales de cliente, estado, prioridad y búsqueda.</summary>
    Task<(IReadOnlyList<Caso> Items, int TotalCount)> GetPagedAsync(
        Guid? clienteId,
        EstadoCaso? estado,
        PrioridadCaso? prioridad,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Registra un nuevo caso en el contexto de persistencia.</summary>
    Task AddAsync(Caso caso, CancellationToken cancellationToken = default);

    /// <summary>Registra explícitamente una nueva respuesta hija en el contexto de persistencia.</summary>
    Task AddRespuestaAsync(Respuesta respuesta, CancellationToken cancellationToken = default);

    /// <summary>Marca un caso existente para eliminación en la unidad de trabajo.</summary>
    void Remove(Caso caso);
}
