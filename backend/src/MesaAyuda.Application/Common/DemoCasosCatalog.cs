using MesaAyuda.Domain.Entities;
using MesaAyuda.Domain.Enums;

namespace MesaAyuda.Application.Common;

/// <summary>
/// Catálogo reutilizable de casos de demostración del ERP Ofima para seeding inicial y restauración.
/// </summary>
public static class DemoCasosCatalog
{
    /// <summary>
    /// Construye la lista canónica de 3 casos de demostración con respuestas e historial representativo.
    /// </summary>
    public static IReadOnlyList<Caso> CreateDemoCases(
        Guid cliente1Id,
        Guid cliente2Id,
        string analistaNombre)
    {
        var casoAbierto = new Caso(
            titulo: "Error FAC-402 al intentar anular factura electrónica aceptada por DIAN",
            descripcion: "Al intentar anular directamente una factura de venta en estado Aceptada por DIAN, el sistema arroja el código FAC-402. Necesitamos saber el procedimiento oficial para revertirla.",
            prioridad: PrioridadCaso.Alta,
            clienteId: cliente1Id);

        var casoEnProceso = new Caso(
            titulo: "Cierre contable mensual bloqueado por error CON-301 y cuenta 999905",
            descripcion: "Al ejecutar el cierre de período de octubre el ERP reporta el error CON-301 y muestra saldo pendiente en la cuenta puente 999905.",
            prioridad: PrioridadCaso.Critica,
            clienteId: cliente2Id);
        casoEnProceso.AgregarRespuestaHumana(
            texto: "Estamos revisando en Contabilidad > Consultas > Auditoría de Comprobantes Pendientes los documentos en estado Borrador/Descuadrado y el auxiliar por tercero de la cuenta 999905 para reclasificar la diferencia antes de cerrar el período.",
            autorNombre: analistaNombre);

        var casoResuelto = new Caso(
            titulo: "Desbloqueo de cuenta de usuario tras error SEC-423",
            descripcion: "La cuenta del auxiliar de nómina quedó bloqueada con código SEC-423 tras superar los intentos fallidos de acceso con 2FA.",
            prioridad: PrioridadCaso.Media,
            clienteId: cliente1Id);
        casoResuelto.AgregarRespuestaHumana(
            texto: "Se verificó la identidad del usuario y se ejecutó el desbloqueo en Administración > Seguridad > Desbloqueo de Usuarios dejando trazabilidad del motivo y reinicio de contraseña.",
            autorNombre: analistaNombre);
        casoResuelto.CambiarEstado(EstadoCaso.Resuelto);

        return new[] { casoAbierto, casoEnProceso, casoResuelto };
    }
}
