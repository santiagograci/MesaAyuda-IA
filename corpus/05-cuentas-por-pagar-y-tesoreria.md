# Manual de Cuentas por Pagar, Retenciones y Tesorería

Este manual detalla el proceso de causación de facturas de proveedores con cálculo tributario colombiano, la generación de archivos planos bancarios para pagos masivos y el cruce automático de conciliación bancaria en **Ofima ERP**.

## Causación de Facturas de Proveedores y Retenciones

### Alcance y Reglas Tributarias

Toda factura de compra de bienes o prestación de servicios recibida de proveedores nacionales debe causarse asociando el comprobante electrónico validado y los eventos RADIAN correspondientes (acuse de recibo y recibo del bien o servicio cuando sea operación a crédito). El ERP liquida automáticamente las retenciones según el perfil fiscal del proveedor y la parametrización del concepto de compra:

- **Retención en la Fuente (Renta):** Evaluación automática de la base mínima en **UVT** vigente (por ejemplo, compras generales desde 27 UVT al 2.5% o 3.5%, servicios desde 4 UVT al 4% o 6%, honorarios 10% u 11%).
- **ReteICA:** Cálculo por municipio de prestación del servicio o entrega, tarifa por mil y código de actividad económica **CIIU** del proveedor.
- **ReteIVA:** Aplicación del **15%** sobre el valor del IVA facturado cuando el adquirente es Gran Contribuyente o responsable de IVA frente a proveedores aplicables según estatuto tributario.
- Las cuentas contables afectadas por defecto corresponden a Proveedores (`2205` / `2335`) y pasivos por retenciones (`2365`, `2367` y `2368`).

### Procedimiento Operativo

1. Ingrese a **`Compras > Documentos > Causación de Factura`**.
2. Seleccione el proveedor, digite o importe desde el XML el número de factura electrónica, la fecha de emisión y el **CUFE** del proveedor.
3. Si existe orden de compra o entrada de almacén previa, vincúlela para conciliar cantidades y costos automáticamente; de lo contrario, seleccione la cuenta de gasto o activo y el centro de costo.
4. Revise la pestaña **Impuestos y Retenciones** donde Ofima ERP muestra la base en UVT, Retefuente, ReteICA y ReteIVA calculados.
5. Haga clic en **Contabilizar Causación** para dejar la factura habilitada en la programación de cartera por pagar.

## Generación de Pagos Masivos (Archivo Plano ACH)

### Alcance y Reglas de Control

Para minimizar errores de digitación y riesgos de fraude, los pagos a proveedores y nómina se procesan mediante lotes electrónicos ACH con segregación de funciones: un **Analista de Tesorería** prepara el lote y un aprobador financiero autorizado confirma la dispersión con **2FA TOTP**.

- Solo pueden incluirse facturas causadas, aprobadas y no marcadas en estado de bloqueo o glosa.
- El proveedor debe tener cuenta bancaria activa, tipo de cuenta (Ahorros o Corriente), código de banco ACH y número de identificación verificados.

### Procedimiento Operativo

1. Ingrese a **`Tesorería > Egresos > Generación Archivo Plano Bancario`**.
2. Seleccione la cuenta bancaria pagadora de la compañía y la fecha de dispersión.
3. Filtre las facturas por pagar vencidas o próximas a vencer, marque los documentos a cancelar (pago total o abono) y verifique el valor total del lote.
4. Seleccione el formato de salida requerido por la entidad financiera: **`Bancolombia PAB`** (Pago a Proveedores / Nómina) o **`Davivienda`** (Archivo Plano ACH).
5. Haga clic en **Generar Lote y Archivo Plano** para descargar el archivo `.txt` validado según estructura bancaria y cárguelo en el portal empresarial del banco.
6. Una vez el banco confirme la aplicación exitosa del lote, regrese al módulo y ejecute **Confirmar Egresos del Lote** para emitir automáticamente los Comprobantes de Egreso (`CE`) y descargar la cartera por pagar.

## Conciliación Bancaria Automática

### Alcance y Formatos Soportados

La conciliación bancaria garantiza que los saldos de las cuentas `1110` en Ofima ERP coincidan con el extracto oficial de cada entidad financiera antes de ejecutar el cierre contable mensual (ver *Manual de Cierre Contable Mensual, Anual y Conciliación*).

- **Formatos soportados:** El motor de importación acepta archivos planos estándar **`.csv`** (delimitados por coma o punto y coma) y extractos bancarios estructurados **`MT940`** (SWIFT).

### Procedimiento Operativo

1. Descargue el extracto oficial del banco en formato **`.csv`** o **`MT940`** correspondiente al mes a conciliar.
2. Ingrese a **`Tesorería > Conciliación > Cruce Automático`**, seleccione el banco, la cuenta contable y el período.
3. Cargue el archivo del extracto mediante **Importar Extracto Bancario** y verifique que el saldo inicial del extracto coincida con el saldo final conciliado del mes anterior.
4. Ejecute **Correr Reglas de Cruce Automático**; el sistema emparejará automáticamente los movimientos por fecha valor, monto exacto y número de referencia bancaria.
5. Revise las **partidas conciliatorias pendientes**:
   - **Gastos bancarios no registrados** (cuota de manejo, comisiones ACH, **GMF 4x1000**, intereses): genérelos directamente desde la pantalla como notas débito/crédito contables.
   - **Partidas en tránsito** (cheques entregados no cobrados o consignaciones de último día): déjelas marcadas como partidas en tránsito para cruce del mes siguiente.
6. Cuando la diferencia neta sea exactamente `$0.00`, haga clic en **Cerrar Conciliación Mensual**.
