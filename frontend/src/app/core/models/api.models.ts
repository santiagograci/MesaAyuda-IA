/** Estados válidos del ciclo de vida de un caso de soporte. */
export type EstadoCaso = 'Abierto' | 'EnProceso' | 'Resuelto' | 'Cerrado';

/** Niveles de cumplimiento SLA calculados por el backend. */
export type EstadoSla = 'Cumplido' | 'EnTiempo' | 'PorVencer' | 'Vencido';

/** Prioridades operativas disponibles para clasificar un caso. */
export type PrioridadCaso = 'Baja' | 'Media' | 'Alta' | 'Critica';

/** Guía operativa descriptiva de cada nivel de prioridad en MesaAyuda IA. */
export const PRIORIDAD_INFO: Record<PrioridadCaso, { etiqueta: string; descripcion: string }> = {
  Baja: {
    etiqueta: 'Sin impacto operativo',
    descripcion: 'Consultas generales, dudas de configuración o solicitudes sin impacto operativo.'
  },
  Media: {
    etiqueta: 'Impacto moderado',
    descripcion: 'Fallo funcional menor que permite continuar trabajando con alternativa temporal.'
  },
  Alta: {
    etiqueta: 'Proceso clave afectado',
    descripcion: 'Proceso clave afectado (facturación electrónica, nómina o inventarios) sin alternativa directa.'
  },
  Critica: {
    etiqueta: 'Operación crítica detenida',
    descripcion: 'Producción detenida, bloqueo de cierre contable/DIAN o caída total del ERP.'
  }
};

/** Orden estándar de prioridades para formularios de selección. */
export const PRIORIDAD_LIST: readonly PrioridadCaso[] = ['Baja', 'Media', 'Alta', 'Critica'];

/** Roles autorizados dentro de la plataforma MesaAyuda IA. */
export type RolUsuario = 'Cliente' | 'Analista';

/** Modo visual de la interfaz. */
export type ThemeMode = 'light' | 'dark';

/** Definición de una máscara cromática predefinida o personalizada. */
export interface ThemeMask {
  id: string;
  nombre: string;
  accentHex: string;
  radiusPx: number;
}

/** Credenciales enviadas en el inicio de sesión. */
export interface LoginRequest {
  email: string;
  password: string;
}

/** Respuesta del endpoint POST /api/auth/login con token JWT. */
export interface LoginResponse {
  token: string;
  userId: string;
  nombre: string;
  email: string;
  rol: RolUsuario;
}

/** Usuario de demostración devuelto por GET /api/auth/demo-users. */
export interface DemoUser {
  nombre: string;
  email: string;
  rol: RolUsuario;
}

/** Fragmento documental citado por el motor RAG. */
export interface FuenteCitada {
  documento: string;
  seccion: string;
  fragmento: string;
  puntaje: number;
}

/** Respuesta humana o sugerencia de IA asociada a un caso. */
export interface Respuesta {
  id: string;
  texto: string;
  esGeneradaPorIA: boolean;
  aprobadaPorAnalista: boolean;
  autorNombre: string;
  fuentesCitadas: FuenteCitada[];
  fechaCreacion: string;
  imagenAdjuntaBase64?: string | null;
}

/** Vista resumida de un caso en listados paginados. */
export interface CasoResumen {
  id: string;
  titulo: string;
  prioridad: PrioridadCaso;
  estado: EstadoCaso;
  fechaCreacion: string;
  clienteNombre: string;
  totalRespuestas: number;
  tieneSugerenciaPendiente: boolean;
  fechaLimiteSla: string;
  estadoSla: EstadoSla;
}

/** Vista detallada de un caso con historial completo de respuestas. */
export interface CasoDetalle {
  id: string;
  titulo: string;
  descripcion: string;
  prioridad: PrioridadCaso;
  estado: EstadoCaso;
  fechaCreacion: string;
  clienteId: string;
  clienteNombre: string;
  respuestas: Respuesta[];
  fechaLimiteSla: string;
  estadoSla: EstadoSla;
  calificacionCsat: number | null;
  comentarioCsat: string | null;
  imagenAdjuntaBase64?: string | null;
}

/** Resultado paginado genérico alineado con PagedResult<T> de .NET 8. */
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/** Payload para crear un nuevo caso. */
export interface CrearCasoRequest {
  titulo: string;
  descripcion: string;
  prioridad: PrioridadCaso;
  imagenAdjuntaBase64?: string | null;
}

/** Payload para actualizar un caso existente. */
export interface ActualizarCasoRequest {
  titulo: string;
  descripcion: string;
  prioridad: PrioridadCaso;
}

/** Payload para ejecutar una transición de estado. */
export interface CambiarEstadoRequest {
  nuevoEstado: EstadoCaso;
}

/** Payload para agregar una respuesta humana. */
export interface CrearRespuestaRequest {
  texto: string;
  imagenAdjuntaBase64?: string | null;
}

/** Payload del analista para aprobar y publicar una sugerencia IA. */
export interface AprobarSugerenciaRequest {
  textoFinal: string;
}

/** Estructura RFC 7807 ProblemDetails devuelta por GlobalExceptionHandler. */
export interface ProblemDetailsResponse {
  status?: number;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Vista resumida de un caso almacenado en la papelera de reciclaje. */
export interface CasoEliminado {
  id: string;
  titulo: string;
  prioridad: PrioridadCaso;
  estado: EstadoCaso;
  fechaCreacion: string;
  fechaEliminacion: string | null;
  clienteNombre: string;
}

/** Respuesta devuelta al restaurar casos de demostración. */
export interface RestaurarDemoResponse {
  casosCreados: number;
  mensaje: string;
}

/** Payload para escalar o ajustar la prioridad de un caso con justificación. */
export interface CambiarPrioridadRequest {
  nuevaPrioridad: PrioridadCaso;
  justificacion: string;
}

/** KPIs operativos y consumo FinOps del motor RAG en MesaAyuda IA. */
export interface MetricasMesaAyuda {
  totalCasos: number;
  casosAbiertos: number;
  casosEnProceso: number;
  casosResueltos: number;
  casosCerrados: number;
  casosCriticosActivos: number;
  sugerenciasIaGeneradas: number;
  sugerenciasIaAprobadas: number;
  tasaAdopcionIaPorcentaje: number;
  tokensTotalesConsumidos: number;
  promedioCsat: number | null;
  totalEncuestasCsat: number;
  totalDeflexionesEvitadas: number;
}

/** Resultado devuelto al registrar un ticket evitado mediante deflexión RAG. */
export interface RegistrarDeflexionResponse {
  totalDeflexionesEvitadas: number;
}

/** Payload para reabrir un caso Resuelto o Cerrado con motivo obligatorio. */
export interface ReabrirCasoRequest {
  motivo: string;
}

/** Solicitud de deflexión preventiva antes de crear un ticket. */
export interface DeflexionRequest {
  titulo: string;
  descripcion: string;
}

/** Respuesta devuelta por POST /api/casos/deflexion. */
export interface DeflexionResponse {
  tieneSugerencia: boolean;
  respuestaSugerida: string;
  fuentes: FuenteCitada[];
}

/** Encuesta CSAT enviada por el cliente propietario tras resolver o cerrar un caso. */
export interface RegistrarCsatRequest {
  calificacion: number;
  comentario?: string;
}

/** Eventos emitidos por el flujo SSE POST /api/casos/{id}/sugerencia/stream. */
export type RagStreamEvent =
  | { type: 'sources'; fuentes: FuenteCitada[]; tokensUsados: number }
  | { type: 'token'; chunk: string }
  | { type: 'done'; caso: CasoDetalle }
  | { type: 'error'; detail: string };

/** Manual indexado en la base vectorial RAG y su cantidad de fragmentos. */
export interface DocumentoIndexado {
  documento: string;
  fragmentos: number;
}

/** Inventario completo devuelto por GET /api/documentos. */
export interface DocumentosRagResponse {
  documentos: DocumentoIndexado[];
  totalDocumentos: number;
  totalFragmentos: number;
}

/** Resultado devuelto por POST /api/documentos/ingestar. */
export interface IngestaArchivoResponse {
  archivo: string;
  fragmentosArchivo: number;
  totalDocumentos: number;
  totalFragmentos: number;
  estado: string;
}
