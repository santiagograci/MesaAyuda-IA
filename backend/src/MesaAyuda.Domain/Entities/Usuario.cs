using MesaAyuda.Domain.Enums;
using MesaAyuda.Domain.Exceptions;

namespace MesaAyuda.Domain.Entities;

/// <summary>
/// Representa un usuario autenticable de la plataforma MesaAyuda IA.
/// </summary>
public class Usuario
{
    /// <summary>Identificador único del usuario.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre completo y área del usuario.</summary>
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Correo electrónico corporativo normalizado en minúsculas.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Hash PBKDF2 de la contraseña del usuario.</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>Rol asignado dentro de la mesa de ayuda.</summary>
    public RolUsuario Rol { get; private set; }

    /// <summary>Fecha de creación del usuario en formato UTC.</summary>
    public DateTime FechaCreacion { get; private set; }

    private Usuario()
    {
    }

    /// <summary>
    /// Crea un nuevo usuario validando nombre, correo electrónico y hash de contraseña.
    /// </summary>
    public Usuario(string nombre, string email, string passwordHash, RolUsuario rol)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainException("El nombre del usuario es obligatorio.");
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("El correo electrónico del usuario es obligatorio.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("El hash de contraseña del usuario es obligatorio.");

        Id = Guid.NewGuid();
        Nombre = nombre.Trim();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        Rol = rol;
        FechaCreacion = DateTime.UtcNow;
    }
}
