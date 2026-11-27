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

        var casoAbierto = new Caso(
            titulo: "Error FAC-402 al intentar anular factura electrónica aceptada por DIAN",
            descripcion: "Al intentar anular directamente una factura de venta en estado Aceptada por DIAN, el sistema arroja el código FAC-402. Necesitamos saber el procedimiento oficial para revertirla.",
            prioridad: PrioridadCaso.Alta,
            clienteId: cliente1.Id);

        var casoEnProceso = new Caso(
            titulo: "Cierre contable mensual bloqueado por error CON-301 y cuenta 999905",
            descripcion: "Al ejecutar el cierre de período de octubre el ERP reporta el error CON-301 y muestra saldo pendiente en la cuenta puente 999905.",
            prioridad: PrioridadCaso.Critica,
            clienteId: cliente2.Id);
        casoEnProceso.AgregarRespuestaHumana(
            texto: "Estamos revisando en Contabilidad > Consultas > Auditoría de Comprobantes Pendientes los documentos en estado Borrador/Descuadrado y el auxiliar por tercero de la cuenta 999905 para reclasificar la diferencia antes de cerrar el período.",
            autorNombre: analista.Nombre);

        var casoResuelto = new Caso(
            titulo: "Desbloqueo de cuenta de usuario tras error SEC-423",
            descripcion: "La cuenta del auxiliar de nómina quedó bloqueada con código SEC-423 tras superar los intentos fallidos de acceso con 2FA.",
            prioridad: PrioridadCaso.Media,
            clienteId: cliente1.Id);
        casoResuelto.AgregarRespuestaHumana(
            texto: "Se verificó la identidad del usuario y se ejecutó el desbloqueo en Administración > Seguridad > Desbloqueo de Usuarios dejando trazabilidad del motivo y reinicio de contraseña.",
            autorNombre: analista.Nombre);
        casoResuelto.CambiarEstado(EstadoCaso.Resuelto);

        await dbContext.Casos.AddRangeAsync(
            new[] { casoAbierto, casoEnProceso, casoResuelto },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
