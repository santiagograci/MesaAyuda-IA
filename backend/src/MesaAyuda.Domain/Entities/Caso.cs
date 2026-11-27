using MesaAyuda.Domain.Enums;
using MesaAyuda.Domain.Exceptions;

namespace MesaAyuda.Domain.Entities;

/// <summary>
/// Agregado raíz que modela un caso de soporte empresarial con sus invariantes y máquina de estados.
/// </summary>
public class Caso
{
    private readonly List<Respuesta> _respuestas = new();

    /// <summary>Identificador único del caso.</summary>
    public Guid Id { get; private set; }

    /// <summary>Asunto breve del incidente o solicitud.</summary>
    public string Titulo { get; private set; } = string.Empty;

    /// <summary>Descripción detallada del problema reportado por el cliente.</summary>
    public string Descripcion { get; private set; } = string.Empty;

    /// <summary>Prioridad operativa asignada al caso.</summary>
    public PrioridadCaso Prioridad { get; private set; }

    /// <summary>Estado actual dentro del flujo de atención del caso.</summary>
    public EstadoCaso Estado { get; private set; }

    /// <summary>Fecha de creación del caso en formato UTC.</summary>
    public DateTime FechaCreacion { get; private set; }

    /// <summary>Indica si el caso fue enviado a la papelera mediante borrado lógico.</summary>
    public bool Eliminado { get; private set; }

    /// <summary>Fecha UTC en que el caso fue marcado como eliminado, o nulo si está activo.</summary>
    public DateTime? FechaEliminacion { get; private set; }

    /// <summary>Identificador del usuario cliente propietario del caso.</summary>
    public Guid ClienteId { get; private set; }

    /// <summary>Referencia de navegación al cliente propietario del caso.</summary>
    public Usuario? Cliente { get; private set; }

    /// <summary>Colección de solo lectura de respuestas y borradores IA asociados al caso.</summary>
    public IReadOnlyCollection<Respuesta> Respuestas => _respuestas.AsReadOnly();

    private Caso()
    {
    }

    /// <summary>
    /// Crea un nuevo caso en estado inicial <see cref="EstadoCaso.Abierto"/>.
    /// </summary>
    public Caso(string titulo, string descripcion, PrioridadCaso prioridad, Guid clienteId)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new DomainException("El título del caso es obligatorio.");
        if (string.IsNullOrWhiteSpace(descripcion))
            throw new DomainException("La descripción del caso es obligatoria.");
        if (clienteId == Guid.Empty)
            throw new DomainException("El identificador del cliente es obligatorio.");

        Id = Guid.NewGuid();
        Titulo = titulo.Trim();
        Descripcion = descripcion.Trim();
        Prioridad = prioridad;
        Estado = EstadoCaso.Abierto;
        FechaCreacion = DateTime.UtcNow;
        ClienteId = clienteId;
    }

    /// <summary>
    /// Ejecuta una transición controlada de estado validando la máquina de estados secuencial
    /// y la existencia de al menos una respuesta publicada al cerrar.
    /// </summary>
    public void CambiarEstado(EstadoCaso nuevoEstado)
    {
        EnsureNotDeleted();

        if (Estado == nuevoEstado)
            return;

        if (Estado == EstadoCaso.Cerrado)
            throw new DomainException("Un caso en estado Cerrado no admite cambios de estado.");

        var transicionValida = (Estado, nuevoEstado) switch
        {
            (EstadoCaso.Abierto, EstadoCaso.EnProceso) => true,
            (EstadoCaso.EnProceso, EstadoCaso.Resuelto) => true,
            (EstadoCaso.Resuelto, EstadoCaso.EnProceso) => true,
            (EstadoCaso.Resuelto, EstadoCaso.Cerrado) => true,
            _ => false
        };

        if (!transicionValida)
        {
            throw new DomainException(
                $"Transición de estado inválida de '{Estado}' a '{nuevoEstado}'.");
        }

        if (nuevoEstado == EstadoCaso.Cerrado)
        {
            var tieneRespuestaPublicada = _respuestas.Any(
                r => !r.EsGeneradaPorIA || r.AprobadaPorAnalista);

            if (!tieneRespuestaPublicada)
            {
                throw new DomainException(
                    "No se puede cerrar un caso sin al menos una respuesta publicada.");
            }
        }

        Estado = nuevoEstado;
    }

    /// <summary>
    /// Agrega una respuesta humana publicada y transiciona automáticamente el caso de Abierto a EnProceso.
    /// </summary>
    public Respuesta AgregarRespuestaHumana(string texto, string autorNombre)
    {
        EnsureNotDeleted();

        if (Estado == EstadoCaso.Cerrado)
            throw new DomainException("No se pueden agregar respuestas a un caso ya Cerrado.");

        var respuesta = new Respuesta(
            casoId: Id,
            texto: texto,
            esGeneradaPorIA: false,
            aprobadaPorAnalista: true,
            autorNombre: autorNombre);

        _respuestas.Add(respuesta);

        if (Estado == EstadoCaso.Abierto)
            Estado = EstadoCaso.EnProceso;

        return respuesta;
    }

    /// <summary>
    /// Registra un borrador de respuesta sugerido por IA pendiente de aprobación humana.
    /// </summary>
    public Respuesta AgregarSugerenciaIA(string texto, string fuentesJson)
    {
        EnsureNotDeleted();

        if (Estado == EstadoCaso.Cerrado)
            throw new DomainException("No se pueden generar sugerencias de IA en un caso Cerrado.");

        var sugerencia = new Respuesta(
            casoId: Id,
            texto: texto,
            esGeneradaPorIA: true,
            aprobadaPorAnalista: false,
            autorNombre: "MesaAyuda IA",
            fuentesCitadasJson: fuentesJson);

        _respuestas.Add(sugerencia);
        return sugerencia;
    }

    /// <summary>
    /// Actualiza título, descripción y prioridad de un caso que no se encuentre Cerrado.
    /// </summary>
    public void Actualizar(string titulo, string descripcion, PrioridadCaso prioridad)
    {
        EnsureNotDeleted();

        if (Estado == EstadoCaso.Cerrado)
            throw new DomainException("No se puede modificar un caso que ya se encuentra Cerrado.");
        if (string.IsNullOrWhiteSpace(titulo))
            throw new DomainException("El título del caso es obligatorio.");
        if (string.IsNullOrWhiteSpace(descripcion))
            throw new DomainException("La descripción del caso es obligatoria.");

        Titulo = titulo.Trim();
        Descripcion = descripcion.Trim();
        Prioridad = prioridad;
    }

    /// <summary>
    /// Aprueba y opcionalmente edita una sugerencia generada por IA para publicarla al cliente.
    /// </summary>
    public Respuesta AprobarSugerenciaIA(Guid respuestaId, string textoFinal, string nombreAnalista)
    {
        EnsureNotDeleted();

        if (Estado == EstadoCaso.Cerrado)
            throw new DomainException("No se pueden aprobar respuestas en un caso Cerrado.");

        var respuesta = _respuestas.FirstOrDefault(r => r.Id == respuestaId)
            ?? throw new KeyNotFoundException($"No se encontró la respuesta '{respuestaId}' en el caso '{Id}'.");

        if (!respuesta.EsGeneradaPorIA)
            throw new DomainException("Solo las sugerencias generadas por IA requieren aprobación de analista.");

        respuesta.AprobarYEditar(textoFinal, nombreAnalista);

        if (Estado == EstadoCaso.Abierto)
            Estado = EstadoCaso.EnProceso;

        return respuesta;
    }

    /// <summary>
    /// Marca el caso como eliminado (borrado lógico) registrando la fecha UTC de envío a papelera.
    /// </summary>
    public void MarcarComoEliminado()
    {
        if (Eliminado)
            throw new DomainException("El caso ya se encuentra en la papelera de reciclaje.");

        Eliminado = true;
        FechaEliminacion = DateTime.UtcNow;
    }

    /// <summary>
    /// Restaura el caso desde la papelera de reciclaje hacia la bandeja activa.
    /// </summary>
    public void Restaurar()
    {
        if (!Eliminado)
            throw new DomainException("El caso no se encuentra en la papelera de reciclaje.");

        Eliminado = false;
        FechaEliminacion = null;
    }

    private void EnsureNotDeleted()
    {
        if (Eliminado)
            throw new DomainException("No se puede modificar un caso que se encuentra en la papelera.");
    }
}
