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

    /// <summary>Fecha UTC de la última actualización operativa del caso.</summary>
    public DateTime? FechaActualizacion { get; private set; }

    /// <summary>Calificación de satisfacción CSAT (1 a 5) otorgada por el cliente al resolver/cerrar.</summary>
    public int? CalificacionCsat { get; private set; }

    /// <summary>Comentario opcional registrado por el cliente junto con la encuesta CSAT.</summary>
    public string? ComentarioCsat { get; private set; }

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
    public Respuesta AgregarSugerenciaIA(string texto, string fuentesJson, int tokensUsados = 0)
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
            fuentesCitadasJson: fuentesJson,
            tokensUsados: tokensUsados);

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

    /// <summary>
    /// Ajusta o escala la prioridad del caso validando las restricciones por rol de usuario.
    /// </summary>
    public void CambiarPrioridad(PrioridadCaso nuevaPrioridad, string rolUsuario)
    {
        EnsureNotDeleted();

        if (Estado == EstadoCaso.Cerrado)
            throw new DomainException("No se puede cambiar la prioridad de un caso Cerrado.");

        if (!Enum.IsDefined(nuevaPrioridad))
            throw new DomainException("La prioridad especificada no es válida.");

        if (nuevaPrioridad == Prioridad)
            throw new DomainException("La nueva prioridad debe ser diferente a la actual.");

        var esCliente = string.Equals(rolUsuario, nameof(RolUsuario.Cliente), StringComparison.OrdinalIgnoreCase);
        var esAnalista = string.Equals(rolUsuario, nameof(RolUsuario.Analista), StringComparison.OrdinalIgnoreCase);

        if (!esCliente && !esAnalista)
            throw new DomainException("El rol actual no está autorizado para cambiar la prioridad del caso.");

        if (esCliente && nuevaPrioridad <= Prioridad)
            throw new DomainException("El cliente solo puede elevar la prioridad del caso hacia un nivel superior.");

        Prioridad = nuevaPrioridad;
        FechaActualizacion = DateTime.UtcNow;
    }

    /// <summary>
    /// Registra una entrada de auditoría visible en el historial del caso sin alterar su estado actual.
    /// </summary>
    public Respuesta RegistrarAuditoria(string texto, string autorNombre)
    {
        EnsureNotDeleted();

        if (Estado == EstadoCaso.Cerrado)
            throw new DomainException("No se pueden registrar eventos en un caso Cerrado.");

        var evento = new Respuesta(
            casoId: Id,
            texto: texto,
            esGeneradaPorIA: false,
            aprobadaPorAnalista: true,
            autorNombre: autorNombre);

        _respuestas.Add(evento);
        return evento;
    }

    /// <summary>
    /// Retorna la ventana objetivo de resolución (SLA) según la prioridad del caso.
    /// </summary>
    public static TimeSpan ObtenerVentanaSla(PrioridadCaso prioridad) => prioridad switch
    {
        PrioridadCaso.Critica => TimeSpan.FromHours(2),
        PrioridadCaso.Alta => TimeSpan.FromHours(8),
        PrioridadCaso.Media => TimeSpan.FromHours(24),
        PrioridadCaso.Baja => TimeSpan.FromHours(72),
        _ => TimeSpan.FromHours(24)
    };

    /// <summary>
    /// Calcula la fecha límite UTC de cumplimiento SLA a partir de la creación y prioridad.
    /// </summary>
    public DateTime CalcularFechaLimiteSla() => FechaCreacion.Add(ObtenerVentanaSla(Prioridad));

    /// <summary>
    /// Evalúa el estado actual del SLA (<c>Cumplido</c>, <c>EnTiempo</c>, <c>PorVencer</c> o <c>Vencido</c>).
    /// </summary>
    public string EvaluarEstadoSla(DateTime? ahoraUtc = null)
    {
        if (Estado is EstadoCaso.Resuelto or EstadoCaso.Cerrado)
            return "Cumplido";

        var ahora = ahoraUtc ?? DateTime.UtcNow;
        var ventana = ObtenerVentanaSla(Prioridad);
        var limite = FechaCreacion.Add(ventana);

        if (ahora >= limite)
            return "Vencido";

        var restante = limite - ahora;
        return restante.TotalSeconds <= ventana.TotalSeconds * 0.25
            ? "PorVencer"
            : "EnTiempo";
    }

    /// <summary>
    /// Registra la calificación CSAT (1 a 5) y comentario opcional de un caso resuelto o cerrado.
    /// </summary>
    public void RegistrarCsat(int calificacion, string? comentario)
    {
        EnsureNotDeleted();

        if (Estado is not (EstadoCaso.Resuelto or EstadoCaso.Cerrado))
            throw new DomainException("Solo se puede calificar un caso cuando está Resuelto o Cerrado.");

        if (CalificacionCsat.HasValue)
            throw new DomainException("Este caso ya cuenta con una calificación CSAT registrada.");

        if (calificacion is < 1 or > 5)
            throw new DomainException("La calificación CSAT debe estar entre 1 y 5.");

        var comentarioLimpio = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
        if (comentarioLimpio is not null && comentarioLimpio.Length > 300)
            throw new DomainException("El comentario CSAT no puede superar los 300 caracteres.");

        CalificacionCsat = calificacion;
        ComentarioCsat = comentarioLimpio;
        FechaActualizacion = DateTime.UtcNow;
    }

    private void EnsureNotDeleted()
    {
        if (Eliminado)
            throw new DomainException("No se puede modificar un caso que se encuentra en la papelera.");
    }
}
