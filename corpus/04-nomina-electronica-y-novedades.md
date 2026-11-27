# Manual de Liquidación de Nómina y Nómina Electrónica

Este manual describe los criterios laborales colombianos parametrizados en **Ofima ERP** para el cálculo de nómina, horas extras, incapacidades y la generación y transmisión del Documento Soporte de Pago de Nómina Electrónica (DSNE) ante la DIAN.

## Liquidación de Nómina y Horas Extras

### Porcentajes de Recargos y Reglas de Cálculo

El módulo de nómina calcula automáticamente el valor de la hora ordinaria dividiendo el salario básico mensual entre la jornada mensual legal vigente y aplica los siguientes factores de recargo conforme al Código Sustantivo del Trabajo:

| Concepto de Tiempo Suplementario o Recargo | Recargo Legal | Factor Multiplicador |
| :--- | :--- | :--- |
| **Hora Extra Diurna (HED)** | **25%** sobre hora ordinaria | `1.25` |
| **Hora Extra Nocturna (HEN)** | **75%** sobre hora ordinaria | `1.75` |
| **Recargo Dominical o Festivo** | **75%** adicional sobre hora ordinaria | `1.75` (diurna) |
| **Recargo Nocturno Ordinario** | **35%** sobre hora ordinaria | `0.35` (adicional) |

- El sistema calcula también automáticamente los descuentos de ley a cargo del trabajador: **Salud (4%)**, **Pensión (4%)**, Fondo de Solidaridad Pensional (1% a 2% cuando el IBC supera 4 SMMLV) y Retención en la Fuente por salarios (Procedimiento 1 o 2).

### Procedimiento Operativo

1. Con perfil **Analista de Nómina**, verifique que todas las novedades del período (horas extras, comisiones, préstamos, vacaciones e incapacidades) hayan sido cargadas y aprobadas.
2. Ingrese a **`Nómina > Procesos > Liquidación de Período`**.
3. Seleccione el grupo de nómina (quincenal o mensual), el período a liquidar y haga clic en **Calcular Liquidación Preliminar**.
4. Revise los totales devengados, deducidos y neto a pagar en el reporte de prenómina, verificando el detalle de horas extras (25% / 75%) y recargos dominicales/festivos (75%).
5. Ejecute **Aprobar y Contabilizar Nómina** para generar el comprobante contable de causación y habilitar los archivos de pago en tesorería.

## Registro de Incapacidades y Licencias

### Reglas de Pago (Enfermedad Común y Licencias)

Para ausentismos por incapacidad médica de origen común, Ofima ERP aplica automáticamente las reglas legales colombianas de distribución del pago entre empleador y EPS:

- **Días 1 y 2:** Los asume directamente el **empleador**, liquidándose al **100%** del salario (o conforme a la política interna configurada cuando la empresa reconozca el 100% de los primeros dos días).
- **A partir del Día 3 (días 3 al 90):** Los asume la **EPS** reconociendo el **66.67%** sobre el **IBC** (Ingreso Base de Cotización) del mes anterior, garantizando que el valor diario nunca sea inferior al SMMLV diario vigente.
- **Incapacidades ARL (accidente de trabajo o enfermedad laboral):** Se liquidan al **100%** desde el día siguiente al evento a cargo de la ARL.
- **Licencias:** Las licencias remuneradas (calamidad, luto, maternidad, paternidad) mantienen la continuidad de aportes; las licencias no remuneradas descuentan salario y ajustan los días cotizados en PILA.

### Procedimiento Operativo

1. Ingrese a **`Nómina > Novedades > Registro de Incapacidades y Licencias`**.
2. Seleccione al empleado, el tipo de novedad (`Incapacidad Enfermedad Común`, `Incapacidad ARL`, `Licencia Remunerada` o `Licencia No Remunerada`), la fecha de inicio y la cantidad de días otorgados.
3. Registre el número de incapacidad, la EPS o ARL responsable y el código diagnóstico **CIE-10** adjuntando el soporte médico en PDF.
4. Guarde la novedad; el motor verificará automáticamente si es prórroga de una incapacidad previa (para no reiniciar el conteo de los primeros 2 días) y aplicará el **66.67%** desde el día 3.

## Transmisión de Nómina Electrónica (CUNE)

### Plazos Legales y Reglas DIAN

De acuerdo con la Resolución DIAN 000013, todo empleador debe transmitir el Documento Soporte de Pago de Nómina Electrónica (DSNE) por cada trabajador dentro de los **primeros 10 días hábiles del mes siguiente** al período en el que ocurrieron los pagos o abonos en cuenta.

- Cada documento aceptado recibe un **CUNE** (Código Único de Nómina Electrónica) que valida la deducibilidad fiscal del costo/gasto de nómina en el impuesto de renta.
- Si se detecta un error posterior a la aceptación DIAN, no se debe modificar la liquidación cerrada sin emitir una **Nota de Ajuste de Nómina Electrónica** (`Tipo 1: Reemplazar` o `Tipo 2: Eliminar`) referenciando el **CUNE** del documento original.

### Procedimiento Operativo

1. Una vez cerrados todos los períodos de pago del mes (ambas quincenas si aplica), ingrese a **`Nómina > Electrónica > Envío DSNE DIAN`**.
2. Seleccione el año y mes a reportar y haga clic en **Consolidar Novedades y Devengados del Mes**.
3. Valide que no existan empleados sin correo, sin código de municipio o con conceptos sin homologación DIAN.
4. Ejecute **Generar XML, Firmar y Transmitir a DIAN** dentro de los **10 días hábiles** permitidos.
5. Confirme en pantalla que todos los trabajadores queden en estado **`Aceptado por DIAN`** con su respectivo **CUNE**. Para correcciones posteriores, utilice la pestaña **`Notas de Ajuste DSNE`**, seleccione el CUNE predecesor y transmita el reemplazo o eliminación.
