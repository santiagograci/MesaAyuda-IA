namespace MesaAyuda.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se infringe una regla de negocio o invariante del dominio.
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// Inicializa una nueva instancia con un mensaje descriptivo de la regla infringida.
    /// </summary>
    public DomainException(string message) : base(message)
    {
    }

    /// <summary>
    /// Inicializa una nueva instancia con mensaje descriptivo y excepción interna.
    /// </summary>
    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
