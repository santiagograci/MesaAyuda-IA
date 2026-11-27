/** Estados válidos del ciclo de vida de un caso de soporte. */
export type EstadoCaso = 'Abierto' | 'EnProceso' | 'Resuelto' | 'Cerrado';

/** Prioridades operativas disponibles para clasificar un caso. */
export type PrioridadCaso = 'Baja' | 'Media' | 'Alta' | 'Critica';

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
