# Manual de Cierre Contable Mensual, Anual y Conciliación

Este manual describe los controles previos, el diagnóstico de descuadres, el cierre mensual y anual bajo NIIF (Plan Único de Cuentas colombiano) y el protocolo de reapertura controlada en **Ofima ERP**.

## Diagnóstico cuando No Cuadra el Cierre Contable

### Las 3 Causas Principales de Bloqueo y Descuadre

Antes de ejecutar el cierre de período, el motor contable de Ofima ERP valida la consistencia integral de los libros auxiliares y el libro mayor. Cuando el cierre falla o presenta diferencias, el problema responde casi siempre a una de estas **3 causas principales**:

1. **Comprobantes en estado `Borrador` o `Descuadrado` (Error `CON-301`):** Existen documentos contables manuales o de interfaz (nómina, facturación, compras, tesorería) dentro del rango de fechas del período que quedaron sin contabilizar (`Borrador`) o cuya suma de débitos es distinta a la de créditos (`Descuadrado`). El ERP detiene el proceso lanzando **`CON-301: Existen comprobantes en estado Borrador o Descuadrado que bloquean el cierre del período`**.
2. **Proceso de Diferencia en Cambio no ejecutado:** Para empresas con cuentas por cobrar, cuentas por pagar, bancos o préstamos en moneda extranjera (USD, EUR), la falta de ejecución de la reexpresión a la TRM oficial de cierre de mes (NIC 21) genera diferencias entre los saldos en moneda funcional (COP) y los reportes auxiliares.
3. **Cuentas puente `999905` con saldo distinto de cero:** Las cuentas puente transitorias (específicamente la cuenta **`999905`** usada para interfaces automáticas, traslados entre módulos o migración de saldos iniciales) deben quedar neteadas en cero (`$0.00`) al finalizar cada mes. Un saldo residual indica una contabilización incompleta o una contrapartida pendiente.

### Procedimiento Operativo de Corrección

1. **Resolver `CON-301`:** Ingrese a **`Contabilidad > Consultas > Auditoría de Comprobantes Pendientes`**, filtre por el período en cierre y seleccione los estados `Borrador` y `Descuadrado`. Complete las contrapartidas faltantes, contabilice los borradores válidos o anule documentos duplicados hasta que la consulta devuelva cero registros.
2. **Ejecutar Diferencia en Cambio:** Verifique que la TRM de cierre esté registrada en `Contabilidad > Configuración > Tasas de Cambio`, luego ejecute **`Contabilidad > Procesos > Ajuste por Diferencia en Cambio`** seleccionando el período y generando el comprobante automático de ajuste (ingreso `421020` o gasto `530525`).
3. **Limpiar la cuenta puente `999905`:** Genere el auxiliar por tercero y documento de la cuenta **`999905`** en `Contabilidad > Reportes > Auxiliar por Cuenta`, identifique el origen del saldo pendiente (usualmente una causación parcial de compras o un ingreso de tesorería sin aplicar) y realice el comprobante de reclasificación contra la cuenta definitiva hasta dejar la **`999905`** exactamente en cero.

## Procedimiento de Cierre de Período

### Alcance y Reglas de Negocio

El cierre mensual inmoviliza la contabilidad del período para garantizar la integridad de los estados financieros y las declaraciones tributarias (IVA, Retefuente, ICA). En el mes 12 (diciembre), el cierre anual incluye adicionalmente la cancelación de las cuentas de resultado (clases 4, 5, 6 y 7) y el traslado del resultado del ejercicio a la cuenta patrimonial **`360505`** (Utilidad del ejercicio) o `361005` (Pérdida del ejercicio).

- Todas las conciliaciones bancarias del mes deben encontrarse en estado `Cerrada` (ver *Manual de Cuentas por Pagar, Retenciones y Tesorería*).
- El módulo de inventarios debe haber finalizado su regeneración de costos y ajustes `AJ-INV`.

### Procedimiento Operativo

1. Con perfil **Auxiliar Contable** o **Contador General**, ingrese a **`Contabilidad > Reportes > Balance de Prueba por Terceros`** y verifique que la sumatoria global de Débitos sea igual a la de Créditos, sin terceros en blanco en cuentas que exigen NIT (cruce de retenciones e impuestos).
2. Valide los módulos auxiliares comparando el saldo de cartera (`130505`), proveedores (`220505`), inventarios (`1435`) y bancos (`1110`) contra sus reportes nativos.
3. Acceda a **`Contabilidad > Procesos > Cierre de Período`**.
4. Seleccione el año y el mes a cerrar, y ejecute primero **Pre-validación de Cierre** para confirmar ausencia del error `CON-301`, ejecución de diferencia en cambio y saldo cero en la cuenta **`999905`**.
5. Si el cierre es **anual (mes 12)**, marque la opción **Generar Comprobante de Cancelación de Cuentas de Resultado** y verifique la cuenta destino **`360505`** para el traslado de utilidades.
6. Haga clic en **Ejecutar Cierre Definitivo**. El sistema bloquea automáticamente la creación, edición o anulación de movimientos contables y documentos operativos con fecha del período cerrado.

## Reversión Controlada de Cierre

### Alcance y Reglas de Negocio

La reapertura de un período cerrado constituye una excepción crítica de control interno y auditoría tributaria. Por política de segregación de funciones:

- Únicamente el rol **`Contador General`** con doble factor de autenticación (**2FA TOTP**) activo tiene permisos para ejecutar la reversión.
- Solo puede reabrirse un período de un año fiscal vigente (o mediante autorización especial si ya se presentaron declaraciones ante la DIAN).
- Es obligatorio registrar un motivo detallado que queda almacenado de forma inmutable en el log de auditoría transaccional.

### Procedimiento Operativo

1. Ingrese con el usuario **Contador General** (autenticado con 2FA) a **`Contabilidad > Procesos > Reapertura de Período`**.
2. Seleccione el año y el mes cerrado que requiere reapertura. Si existen períodos posteriores ya cerrados, el ERP exigirá reabrir en orden cronológico inverso para preservar la continuidad de los saldos iniciales.
3. Diligencie el campo obligatorio **Motivo de Reapertura** (mínimo 20 caracteres, referenciando número de ticket o requerimiento de auditoría).
4. Confirme la operación ingresando su código **TOTP** de 6 dígitos. El sistema registra el evento en **`Administración > Auditoría > Log Transaccional`** con usuario, timestamp e IP de origen.
5. Ejecute el ajuste contable requerido y proceda inmediatamente a cerrar nuevamente el período siguiendo la sección *Procedimiento de Cierre de Período*.
