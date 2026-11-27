# Manual de Facturación Electrónica DIAN y Anulaciones

Este manual define los lineamientos funcionales y operativos en **Ofima ERP** para la emisión de facturas electrónicas de venta bajo el esquema de validación previa de la DIAN (Resolución 000042 y Anexo Técnico vigente), el manejo de anulaciones mediante Nota Crédito electrónica y la operación en contingencia tipo 03.

## Emisión y Validación Previa DIAN

### Alcance y Reglas de Negocio

Toda operación de venta de bienes o prestación de servicios gravados, exentos o excluidos debe generarse a través del módulo de facturación electrónica con perfil **Facturador Electrónico**. Antes de autorizar el envío a la DIAN, el ERP ejecuta validaciones obligatorias en línea:

- **Integridad del tercero:** El cliente debe contar con NIT o cédula verificada, dígito de verificación válido, responsabilidad fiscal (ej. `O-13`, `O-15`, `O-23`, `R-99-PN`), régimen tributario y correo electrónico de recepción.
- **Control de inventarios:** Para ítems inventariables, el sistema valida disponibilidad en la bodega asignada; si no hay existencia suficiente, bloquea la emisión con el error `INV-108` (ver *Manual de Control de Inventarios, Kardex y Bodegas*).
- **Resolución de numeración:** La factura consume un consecutivo vigente de la resolución DIAN parametrizada (prefijo, rango autorizado y fecha de vigencia).
- **Impuestos y retenciones:** Cálculo automático de IVA (19%, 5% o 0%), Impuesto Nacional al Consumo (INC) y autorretenciones cuando aplique.

### Procedimiento Operativo

1. Ingrese por la ruta **`Facturación > Documentos > Factura Electrónica de Venta`**.
2. Seleccione el prefijo de facturación electrónica autorizado, la fecha de emisión y el tercero comprador.
3. Agregue las referencias o servicios, confirme cantidades, lista de precios, descuentos e impuestos aplicados por línea.
4. Haga clic en **Guardar y Validar**. El sistema genera el archivo XML bajo el estándar **UBL 2.1** (`Invoice`) con los nodos de extensión DIAN.
5. El motor criptográfico aplica la firma digital **XAdES-BES** utilizando el certificado digital vigente de la compañía y calcula el **CUFE** (Código Único de Factura Electrónica) mediante algoritmo SHA-384.
6. Ofima ERP transmite el XML firmado de manera síncrona al servicio web de validación previa de la DIAN (`SendBillSync`).
7. Al recibir respuesta positiva, el documento cambia a estado **`Aceptada por DIAN`**, almacena el contenedor **`AttachedDocument`** (XML validado + ApplicationResponse) y genera la representación gráfica en PDF con el código QR y el CUFE impreso para envío automático al correo del cliente.

### Códigos de Error y Validaciones

| Código | Descripción | Acción recomendada |
| :--- | :--- | :--- |
| `FAC-401` | Resolución DIAN vencida o numeración agotada. | Solicite ampliación en MUISCA y actualice la resolución en `Facturación > Configuración > Resoluciones DIAN`. |
| `INV-108` | Existencia insuficiente en bodega principal para uno o más ítems. | Verifique saldos o realice un traslado previo en `Inventarios > Movimientos > Traslado entre Bodegas`. |
| `DIAN-99` | Tiempo de espera agotado o indisponibilidad temporal del servicio DIAN. | Active el flujo de Contingencia Tipo 03 si la falla persiste. |

## Reglas de Anulación Directa vs. Nota Crédito

### Prohibición de Anulación Directa (Error FAC-402)

En cumplimiento estricto de la normativa fiscal colombiana, una factura electrónica que ya obtuvo validación previa positiva de la DIAN adquiere firmeza tributaria y fiscal. Por esta razón:

- **Estado `Aceptada por DIAN`:** Nunca puede anularse, eliminarse ni reutilizar su consecutivo directamente en el ERP. Si un usuario intenta ejecutar la acción de anulación directa sobre una factura aceptada, el sistema bloquea la operación y arroja el error **`FAC-402: No es posible anular directamente una factura en estado Aceptada por DIAN; debe emitir una Nota Crédito electrónica NC-01 referenciando el CUFE original`**.
- **Estados `Borrador` o `Rechazada por DIAN`:** Únicamente los documentos que no poseen CUFE validado ni aceptación fiscal pueden ser corregidos o anulados internamente sin emitir Nota Crédito (liberando reservas si no fueron transmitidos).

### Procedimiento Operativo para Nota Crédito Electrónica (NC-01)

1. Ingrese a **`Facturación > Notas > Nota Crédito Electrónica`**.
2. Seleccione el tipo de documento **`NC-01`** (Nota Crédito Electrónica que referencia factura previa).
3. En el campo **Documento Referenciado**, busque y seleccione la factura electrónica original; el sistema cargará automáticamente el número de factura, la fecha de emisión y el **CUFE** original en el nodo `<BillingReference>` del XML UBL 2.1.
4. Seleccione el **Concepto de Corrección DIAN** correspondiente:
   - **`1`**: Devolución parcial de los bienes y/o no aceptación parcial del servicio.
   - **`2`**: Anulación de factura electrónica (reversión total).
   - **`3`**: Rebaja o descuento parcial o total.
   - **`4`**: Ajuste de precio.
5. Verifique las cantidades o valores a acreditar y haga clic en **Emitir y Transmitir a DIAN**.
6. Una vez la Nota Crédito recibe estado **`Aceptada por DIAN`** (con su propio **CUDE**), el ERP revierte automáticamente el saldo en cuentas por cobrar, reingresa las unidades al Kardex (si es concepto `1` o `2` con devolución física) y genera el comprobante contable de reversión de ingresos e IVA.

## Contingencia Tipo 03 y Plazos de Transmisión

### Alcance y Reglas de Negocio

Cuando se presenta una falla técnica en los servicios tecnológicos de la DIAN (debidamente verificada) o una caída de infraestructura local que impide generar y firmar en línea, la empresa puede operar bajo **Contingencia Tipo 03** (Factura de Venta por Talonario o Papel) utilizando una resolución previamente autorizada por la DIAN para contingencia.

- La factura entregada al cliente durante la contingencia tiene plena validez comercial y fiscal mientras dura el evento.
- Está prohibido duplicar consecutivos de la resolución electrónica normal en el talonario de contingencia.
- **Plazo legal obligatorio:** Una vez restablecidos los servicios de la DIAN o la conectividad del facturador, la empresa dispone de un plazo máximo de **48 horas** para transcribir, generar el XML UBL 2.1 (con tipo de factura `03` de contingencia) y transmitirlo a la DIAN.

### Procedimiento Operativo

1. En **`Facturación > Configuración > Modo Contingencia`**, active el estado de contingencia indicando fecha, hora de inicio y motivo (`Caída servicios DIAN` o `Falla tecnológica del facturador`), usando el perfil **Facturador Electrónico** o **Administrador de Seguridad**.
2. Registre las ventas emitidas en talonario mediante **`Facturación > Documentos > Factura de Contingencia Tipo 03`**, capturando exactamente el número de talonario y la fecha/hora real de expedición al cliente.
3. Restablecido el servicio, desactive el modo contingencia y acceda a **`Facturación > Electrónica > Cola de Transmisión Contingencia`**.
4. Seleccione los documentos pendientes, verifique que no hayan superado las **48 horas** desde el restablecimiento y ejecute **Generar XML y Transmitir a DIAN** para obtener el CUFE de transcripción y completar la trazabilidad contable y fiscal.
