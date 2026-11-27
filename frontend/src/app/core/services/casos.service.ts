import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, NgZone, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ActualizarCasoRequest,
  AprobarSugerenciaRequest,
  CambiarEstadoRequest,
  CambiarPrioridadRequest,
  CasoDetalle,
  CasoEliminado,
  CasoResumen,
  CrearCasoRequest,
  CrearRespuestaRequest,
  DeflexionRequest,
  DeflexionResponse,
  EstadoCaso,
  FuenteCitada,
  MetricasMesaAyuda,
  PagedResult,
  PrioridadCaso,
  RagStreamEvent,
  ReabrirCasoRequest,
  RegistrarCsatRequest,
  RegistrarDeflexionResponse,
  Respuesta,
  RestaurarDemoResponse
} from '../models/api.models';
import { AuthService } from './auth.service';

/**
 * Cliente HTTP tipado para todas las operaciones sobre /api/casos.
 */
@Injectable({ providedIn: 'root' })
export class CasosService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly ngZone = inject(NgZone);
  private readonly baseUrl = '/api/casos';

  /** Obtiene una página de casos filtrada opcionalmente por estado, prioridad y búsqueda. */
  listarCasos(
    estado: EstadoCaso | '' = '',
    page = 1,
    pageSize = 8,
    prioridad: PrioridadCaso | '' = '',
    busqueda = ''
  ): Observable<PagedResult<CasoResumen>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (estado) {
      params = params.set('estado', estado);
    }
    if (prioridad) {
      params = params.set('prioridad', prioridad);
    }
    const term = busqueda.trim();
    if (term) {
      params = params.set('busqueda', term);
    }

    return this.http.get<PagedResult<CasoResumen>>(this.baseUrl, { params });
  }

  /** Obtiene el detalle completo de un caso por su UUID. */
  obtenerCaso(id: string): Observable<CasoDetalle> {
    return this.http.get<CasoDetalle>(`${this.baseUrl}/${id}`);
  }

  /** Crea un nuevo caso de soporte en estado inicial Abierto. */
  crearCaso(request: CrearCasoRequest): Observable<CasoDetalle> {
    return this.http.post<CasoDetalle>(this.baseUrl, request);
  }

  /** Actualiza título, descripción y prioridad de un caso no cerrado. */
  actualizarCaso(id: string, request: ActualizarCasoRequest): Observable<CasoDetalle> {
    return this.http.put<CasoDetalle>(`${this.baseUrl}/${id}`, request);
  }

  /** Envía un caso de soporte a la papelera mediante borrado lógico (requiere rol Analista). */
  eliminarCaso(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Lista los casos almacenados en la papelera de reciclaje (exclusivo Analista). */
  listarPapelera(): Observable<CasoEliminado[]> {
    return this.http.get<CasoEliminado[]>(`${this.baseUrl}/papelera`);
  }

  /** Restaura un caso desde la papelera hacia la bandeja activa (exclusivo Analista). */
  restaurarCaso(id: string): Observable<CasoDetalle> {
    return this.http.post<CasoDetalle>(`${this.baseUrl}/${id}/restaurar`, {});
  }

  /** Recrea los casos de demostración faltantes sin duplicar los activos (exclusivo Analista). */
  restaurarDatosDemo(): Observable<RestaurarDemoResponse> {
    return this.http.post<RestaurarDemoResponse>(`${this.baseUrl}/restaurar-demo`, {});
  }

  /** Ejecuta una transición de estado controlada sobre el caso. */
  cambiarEstado(id: string, nuevoEstado: EstadoCaso): Observable<CasoDetalle> {
    const body: CambiarEstadoRequest = { nuevoEstado };
    return this.http.patch<CasoDetalle>(`${this.baseUrl}/${id}/estado`, body);
  }

  /** Escala o ajusta la prioridad del caso registrando justificación en el historial. */
  cambiarPrioridad(
    id: string,
    nuevaPrioridad: PrioridadCaso,
    justificacion: string
  ): Observable<CasoDetalle> {
    const body: CambiarPrioridadRequest = { nuevaPrioridad, justificacion };
    return this.http.patch<CasoDetalle>(`${this.baseUrl}/${id}/prioridad`, body);
  }

  /** Agrega una respuesta humana publicada al historial del caso. */
  agregarRespuesta(
    id: string,
    texto: string,
    imagenAdjuntaBase64?: string | null
  ): Observable<Respuesta> {
    const body: CrearRespuestaRequest = imagenAdjuntaBase64
      ? { texto, imagenAdjuntaBase64 }
      : { texto };
    return this.http.post<Respuesta>(`${this.baseUrl}/${id}/respuestas`, body);
  }

  /** Solicita una sugerencia generada por el motor RAG para el caso indicado. */
  pedirSugerenciaIA(id: string): Observable<Respuesta> {
    return this.http.post<Respuesta>(`${this.baseUrl}/${id}/sugerencia`, {});
  }

  /** Consulta indicadores operativos y consumo FinOps de IA desde GET /api/casos/metricas. */
  obtenerMetricas(): Observable<MetricasMesaAyuda> {
    return this.http.get<MetricasMesaAyuda>(`${this.baseUrl}/metricas`);
  }

  /** Consulta sugerencia preventiva RAG sin registrar un caso en base de datos. */
  consultarDeflexion(titulo: string, descripcion: string): Observable<DeflexionResponse> {
    const body: DeflexionRequest = { titulo, descripcion };
    return this.http.post<DeflexionResponse>(`${this.baseUrl}/deflexion`, body);
  }

  /** Registra en base de datos un caso evitado mediante deflexión preventiva RAG. */
  registrarDeflexionEvitada(): Observable<RegistrarDeflexionResponse> {
    return this.http.post<RegistrarDeflexionResponse>(`${this.baseUrl}/deflexion/registrar`, {});
  }

  /** Reabre un caso Resuelto o Cerrado devolviéndolo a EnProceso con trazabilidad obligatoria. */
  reabrirCaso(id: string, motivo: string): Observable<CasoDetalle> {
    const body: ReabrirCasoRequest = { motivo };
    return this.http.post<CasoDetalle>(`${this.baseUrl}/${id}/reabrir`, body);
  }

  /** Registra calificación CSAT (1-5 estrellas) del cliente propietario del caso. */
  registrarCsat(id: string, calificacion: number, comentario?: string): Observable<CasoDetalle> {
    const body: RegistrarCsatRequest = comentario?.trim()
      ? { calificacion, comentario: comentario.trim() }
      : { calificacion };
    return this.http.post<CasoDetalle>(`${this.baseUrl}/${id}/csat`, body);
  }

  /**
   * Consume en tiempo real el flujo Server-Sent Events (SSE) para generación de sugerencia IA.
   */
  sugerirRespuestaStream(id: string): Observable<RagStreamEvent> {
    return new Observable<RagStreamEvent>((subscriber) => {
      const controller = new AbortController();
      const token = this.authService.getToken();

      const headers: Record<string, string> = {
        Accept: 'text/event-stream',
        'X-Correlation-ID':
          typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
            ? crypto.randomUUID()
            : `sse-${Date.now()}`
      };
      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }

      void (async () => {
        try {
          const response = await fetch(`${this.baseUrl}/${id}/sugerencia/stream`, {
            method: 'POST',
            headers,
            signal: controller.signal
          });

          if (!response.ok || !response.body) {
            throw new Error(`Error HTTP ${response.status} al iniciar streaming IA.`);
          }

          const reader = response.body.getReader();
          const decoder = new TextDecoder('utf-8');
          let buffer = '';

          while (true) {
            const { value, done } = await reader.read();
            if (done) {
              break;
            }

            buffer += decoder.decode(value, { stream: true });
            let sepIdx = buffer.indexOf('\n\n');
            while (sepIdx !== -1) {
              const rawFrame = buffer.slice(0, sepIdx);
              buffer = buffer.slice(sepIdx + 2);
              this.emitParsedSseFrame(rawFrame, subscriber);
              sepIdx = buffer.indexOf('\n\n');
            }
          }

          if (buffer.trim()) {
            this.emitParsedSseFrame(buffer, subscriber);
          }

          this.ngZone.run(() => subscriber.complete());
        } catch (err) {
          if (controller.signal.aborted) {
            return;
          }
          const detail =
            err instanceof Error ? err.message : 'Error de conexión durante el streaming IA.';
          this.ngZone.run(() => subscriber.error(new Error(detail)));
        }
      })();

      return () => {
        controller.abort();
      };
    });
  }

  private emitParsedSseFrame(
    rawFrame: string,
    subscriber: { next: (val: RagStreamEvent) => void }
  ): void {
    const lines = rawFrame.split(/\r?\n/);
    let eventName = 'message';
    const dataLines: string[] = [];

    for (const line of lines) {
      if (line.startsWith('event:')) {
        eventName = line.slice(6).trim();
      } else if (line.startsWith('data:')) {
        dataLines.push(line.slice(5).trim());
      }
    }

    if (dataLines.length === 0) {
      return;
    }

    try {
      const payload = JSON.parse(dataLines.join('\n')) as Record<string, unknown>;
      this.ngZone.run(() => {
        if (eventName === 'sources') {
          subscriber.next({
            type: 'sources',
            fuentes: (payload['fuentes'] as FuenteCitada[]) ?? [],
            tokensUsados: Number(payload['tokensUsados'] ?? 0)
          });
        } else if (eventName === 'token') {
          subscriber.next({
            type: 'token',
            chunk: String(payload['chunk'] ?? '')
          });
        } else if (eventName === 'done') {
          subscriber.next({
            type: 'done',
            caso: payload as unknown as CasoDetalle
          });
        } else if (eventName === 'error') {
          subscriber.next({
            type: 'error',
            detail: String(payload['detail'] ?? 'No se pudo generar la sugerencia IA.')
          });
        }
      });
    } catch {
      // Ignora tramas JSON incompletas.
    }
  }

  /** Aprueba y opcionalmente edita una sugerencia de IA (exclusivo Analista). */
  aprobarSugerenciaIA(
    casoId: string,
    respuestaId: string,
    textoFinal: string
  ): Observable<Respuesta> {
    const body: AprobarSugerenciaRequest = { textoFinal };
    return this.http.put<Respuesta>(
      `${this.baseUrl}/${casoId}/respuestas/${respuestaId}/aprobar`,
      body
    );
  }
}
