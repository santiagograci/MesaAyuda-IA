namespace MesaAyuda.Application.Interfaces;

/// <summary>
/// Contrato para generación y verificación segura de hashes de contraseña.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Genera un hash seguro con salt aleatorio para la contraseña en texto plano.</summary>
    string Hash(string password);

    /// <summary>Verifica si una contraseña en texto plano coincide con su hash almacenado.</summary>
    bool Verify(string password, string passwordHash);
}
