using MesaAyuda.Application.Common;
using MesaAyuda.Application.Interfaces;
using MesaAyuda.Domain.Entities;
using MesaAyuda.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MesaAyuda.Infrastructure.Persistence;

/// <summary>
/// Inicializador idempotente de datos de demostración para usuarios y casos del ERP.
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// Siembra usuarios de demostración y casos iniciales realistas si la base de datos está vacía.
    /// </summary>
    public static async Task SeedAsync(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.Usuarios.AnyAsync(cancellationToken))
            return;

        var passwordHash = passwordHasher.Hash("Ofima2026*");

        var cliente1 = new Usuario(
            nombre: "Carlos Mendoza - Contabilidad",
            email: "cliente@empresa.com",
            passwordHash: passwordHash,
            rol: RolUsuario.Cliente);

        var cliente2 = new Usuario(
            nombre: "Laura Gómez - Tesorería",
            email: "cliente2@empresa.com",
            passwordHash: passwordHash,
            rol: RolUsuario.Cliente);

        var analista = new Usuario(
            nombre: "Santiago Graciano - Analista Soporte",
            email: "analista@ofima.com",
            passwordHash: passwordHash,
            rol: RolUsuario.Analista);

        await dbContext.Usuarios.AddRangeAsync(new[] { cliente1, cliente2, analista }, cancellationToken);

        var demoCasos = DemoCasosCatalog.CreateDemoCases(cliente1.Id, cliente2.Id, analista.Nombre);
        await dbContext.Casos.AddRangeAsync(demoCasos, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
