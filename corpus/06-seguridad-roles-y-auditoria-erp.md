# Manual de Seguridad, Roles de Usuario y Auditoría Transaccional

Este manual define los controles de acceso, la configuración de autenticación multifactor (2FA), la política de bloqueo automático ante intentos fallidos y el uso del registro inmutable de auditoría en **Ofima ERP**.

## Gestión de Perfiles y Doble Factor (2FA)

### Principio de Mínimo Privilegio y Segregación

El acceso a las funcionalidades del ERP se controla mediante **Control de Acceso Basado en Roles (RBAC)**. Ningún usuario operativo debe contar con privilegios de superadministrador; los perfiles deben separarse para evitar conflictos de interés (por ejemplo, quien crea proveedores no debe aprobar pagos masivos, y quien cierra períodos no debe ser el mismo usuario que registra movimientos diarios sin supervisión).

- **Permisos granulares por módulo:** Para cada menú y formulario se asignan capacidades independientes de `Consultar`, `Crear`, `Editar`, `Aprobar`, `Anular` y `Reabrir`.
- **Doble Factor Obligatorio (2FA TOTP):** Todo perfil financiero o administrativo crítico —incluyendo **`Contador General`**, **`Analista de Tesorería`**, **`Administrador de Seguridad`** y aprobadores de lotes o ajustes `AJ-INV`— debe tener activada la autenticación de dos factores mediante código temporal **TOTP** (compatible con Google Authenticator o Microsoft Authenticator).

### Procedimiento Operativo

1. Con perfil **Administrador de Seguridad**, ingrese a **`Administración > Seguridad > Usuarios y Perfiles`**.
2. En la pestaña **Perfiles**, cree o edite el rol correspondiente y marque únicamente las opciones de menú y acciones granulares autorizadas para el cargo.
3. En la pestaña **Usuarios**, cree la cuenta corporativa del colaborador, asigne el perfil y defina las empresas y bodegas permitidas.
4. Active la casilla **Requerir 2FA TOTP Obligatorio** para roles financieros o administrativos. En el siguiente inicio de sesión, el sistema guiará al usuario mediante código QR para vincular su aplicación autenticadora.

## Bloqueo de Cuenta por Intentos Fallidos (Error SEC-423)

### Regla de Protección contra Fuerza Bruta

Para proteger la integridad financiera y prevenir accesos no autorizados por adivinado de contraseñas, Ofima ERP contabiliza los intentos fallidos consecutivos de inicio de sesión (ya sea por contraseña incorrecta o código TOTP inválido).

- Al alcanzar **5 intentos fallidos consecutivos**, la plataforma suspende temporalmente la cuenta y muestra en pantalla el código de error **`SEC-423: Cuenta bloqueada por superar el máximo de 5 intentos fallidos de autenticación. Contacte al Administrador de Seguridad`**.
- El usuario bloqueado no podrá iniciar sesión ni recuperar el acceso por autoservicio hasta que el Administrador verifique su identidad y ejecute el desbloqueo manual.

### Procedimiento Operativo de Desbloqueo

1. El usuario afectado debe reportar el error **`SEC-423`** a la mesa de ayuda indicando su usuario corporativo.
2. El **Administrador de Seguridad** verifica la identidad del solicitante por un canal corporativo autorizado y revisa el origen de los intentos fallidos en el log de seguridad para descartar un ataque externo.
3. Ingrese a **`Administración > Seguridad > Desbloqueo de Usuarios`**.
4. Busque al usuario con estado **`Bloqueado (SEC-423)`**, haga clic en **Desbloquear Cuenta** y registre el motivo del desbloqueo.
5. Si el usuario olvidó su clave o cambió de dispositivo móvil, marque **Forzar Cambio de Contraseña** o **Reiniciar Semilla 2FA TOTP** antes de confirmar.

## Consulta del Log de Auditoría

### Alcance e Inmutabilidad

Ofima ERP mantiene una bitácora transaccional inmutable (solo lectura, sin opción de borrado ni edición incluso para administradores) que registra cada evento crítico ejecutado en la plataforma:

- **Eventos trazados:** Inicio/cierre de sesión, bloqueos `SEC-423`, creación, modificación, aprobación o anulación de documentos, emisión de Notas Crédito `NC-01`, aprobaciones `AJ-INV`, generación de archivos ACH y reaperturas de período contable.
- **Metadatos capturados:** Identificador del usuario, rol activo, **timestamp** exacto en UTC y hora local, **IP de origen**, módulo afectado, identificador del documento y comparación campo a campo (`Valor Anterior` vs. `Valor Nuevo`).

### Procedimiento Operativo

1. Ingrese con perfil **Administrador de Seguridad**, **Auditor** o **Contador General** a **`Administración > Auditoría > Log Transaccional`**.
2. Defina los filtros de búsqueda: rango de fechas y horas, usuario, módulo (`Facturación`, `Contabilidad`, `Inventarios`, `Nómina`, `Tesorería`, `Seguridad`), tipo de acción o número de documento.
3. Haga clic en **Consultar Bitácora** para inspeccionar la línea de tiempo del documento o incidente, validando **IP de origen** y **timestamp** de cada cambio.
4. Para entregar evidencias a Revisoría Fiscal o auditoría interna, utilice **Exportar Evidencia (CSV / PDF Firmado)** con el resumen de integridad del reporte.
