# Manual de Control de Inventarios, Kardex y Bodegas

Este manual establece las reglas de valoración de existencias, el control estricto de saldos por bodega y los procedimientos de conteo físico y ajuste en **Ofima ERP** bajo los lineamientos de la NIC 2 (NIIF para Inventarios).

## Valoración por Promedio Ponderado y Kardex

### Reglas de Valoración y Fórmula

Ofima ERP utiliza el método de **Costo Promedio Ponderado** continuo por referencia y bodega (o consolidado según parametrización contable). Cada entrada por compra o producción recalcula el costo unitario promedio de manera automática mediante la fórmula:

$$\text{Costo Promedio Nuevo} = \frac{(\text{Saldo Unidades} \times \text{Costo Actual}) + (\text{Unidades Entrantes} \times \text{Costo Unitario Compra})}{\text{Saldo Unidades} + \text{Unidades Entrantes}}$$

- **Salidas por venta o consumo:** Se descargan al último costo promedio ponderado vigente al momento de la transacción.
- **Devoluciones en venta (`NC-01`):** Reingresan al inventario al mismo costo histórico con el que salieron en la factura original.
- **Registros extemporáneos:** Si se aprueba una compra o ajuste con fecha anterior a movimientos ya costeados, el usuario debe ejecutar la utilidad de recálculo cronológico antes del cierre contable.

### Consulta de Kardex y Regeneración de Saldos

1. Para auditar movimientos de un producto, ingrese a **`Inventarios > Consultas > Kardex Valorizado`**.
2. Filtre por **Código de Referencia**, **Bodega**, **Rango de Fechas** y tipo de movimiento (compras, ventas, traslados, ajustes). El reporte detalla documento soporte, cantidad de entrada/salida, saldo físico, costo unitario y costo total.
3. Cuando existan documentos ingresados en desorden cronológico o diferencias tras una importación, acceda con perfil **Jefe de Bodega** o **Contador General** a **`Inventarios > Utilidades > Regeneración de Saldos y Costos`**.
4. Seleccione el rango de fechas desde el último mes cerrado, marque **Recalcular Costo Promedio Cronológico** y ejecute el proceso para sincronizar las tablas de saldos con el Kardex y generar el diferencial contable si aplica.

## Bloqueo de Stock Negativo (Error INV-108)

### Causa y Regla de Negocio

Ofima ERP prohíbe estrictamente las existencias negativas en cualquier bodega para evitar distorsiones matemáticas en el promedio ponderado y prevenir inconsistencias fiscales ante la DIAN.

Cuando un **Facturador Electrónico** o auxiliar intenta facturar, remisionar o trasladar una cantidad superior al saldo físico disponible (descontando unidades comprometidas en pedidos reservados), el sistema interrumpe la transacción y devuelve el error **`INV-108: Existencia insuficiente en bodega principal`**.

### Procedimiento Operativo para Resolver INV-108

1. No intente forzar la factura ni modificar manualmente las tablas de saldos. Verifique primero la disponibilidad real ingresando a **`Inventarios > Consultas > Saldos por Bodega`**.
2. Confirme si la mercancía existe físicamente en otra ubicación (por ejemplo, `Bodega Tránsito`, `Bodega Recepción` o `Bodega Regional`) o si hace falta registrar la entrada de compra del proveedor.
3. Si el stock se encuentra en otra bodega del ERP, ingrese a **`Inventarios > Movimientos > Traslado entre Bodegas`**.
4. Seleccione **Bodega Origen** (donde está el saldo disponible) y **Bodega Destino** (bodega principal desde donde se factura).
5. Agregue las referencias, digite las cantidades exactas a trasladar y haga clic en **Aprobar Traslado**. El sistema descarga la bodega origen y acredita la bodega destino al mismo costo promedio sin generar impacto en ingresos ni gastos.
6. Notifique al área de facturación para reintentar la emisión en `Facturación > Documentos > Factura Electrónica de Venta`.

## Ajustes por Conteo Físico

### Alcance y Reglas de Negocio

Las diferencias entre el inventario físico en estantería y el saldo teórico del Kardex (mermas, averías, errores de picking o sobrantes) deben regularizarse exclusivamente mediante documentos de ajuste **`AJ-INV`**.

- Ningún ajuste **`AJ-INV`** afecta existencias ni contabilidad hasta contar con la aprobación explícita del perfil **`Jefe de Bodega`**.
- Los ajustes por faltante descargan la cuenta de inventario (`1435`) contra gasto por baja o diferencia de inventario (`5299` / `5395`), mientras los sobrantes se acreditan según política contable.

### Procedimiento Operativo

1. Ingrese a **`Inventarios > Conteo Físico > Planillas de Toma Cíclica`** y genere un corte congelando la consulta teórica de la bodega o línea seleccionada.
2. Realice el conteo ciego en piso (sin mostrar la cantidad teórica al operario) y digite los resultados en **`Inventarios > Conteo Físico > Registro de Conteos`**.
3. Ejecute **Comparar Físico vs. Sistema** para revisar las variaciones y validar reconteos en ítems con desviación superior al umbral de tolerancia.
4. Haga clic en **Generar Documento de Ajuste (`AJ-INV`)**; el sistema creará automáticamente las líneas de entrada por sobrantes y salida por faltantes en estado `Pendiente de Aprobación`.
5. El **Jefe de Bodega** debe ingresar a **`Inventarios > Movimientos > Aprobación de Ajustes AJ-INV`**, revisar los soportes adjuntos y aprobar el documento para actualizar el Kardex y contabilizar el comprobante correspondiente.
