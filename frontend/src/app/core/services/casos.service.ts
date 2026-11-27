import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ActualizarCasoRequest,
  AprobarSugerenciaRequest,
  CambiarEstadoRequest,
  CasoDetalle,
  CasoResumen,
  CrearCasoRequest,
  CrearRespuestaRequest,
  EstadoCaso,
  PagedResult,
  Respuesta
} from '../models/api.models';

/**
 * Cliente HTTP tipado para todas las operaciones sobre /api/casos.
 */
@Injectable({ providedIn: 'root' })
export class CasosService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/casos';

  /** Obtiene una página de casos filtrada opcionalmente por estado. */
  listarCasos(
    estado: EstadoCaso | '' = '',
    page = 1,
    pageSize = 8
  ): Observable<PagedResult<CasoResumen>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (estado) {
      params = params.set('estado', estado);
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

  /** Elimina un caso de soporte (requiere rol Analista). */
  eliminarCaso(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Ejecuta una transición de estado controlada sobre el caso. */
  cambiarEstado(id: string, nuevoEstado: EstadoCaso): Observable<CasoDetalle> {
    const body: CambiarEstadoRequest = { nuevoEstado };
    return this.http.patch<CasoDetalle>(`${this.baseUrl}/${id}/estado`, body);
  }

  /** Agrega una respuesta humana publicada al historial del caso. */
  agregarRespuesta(id: string, texto: string): Observable<Respuesta> {
    const body: CrearRespuestaRequest = { texto };
    return this.http.post<Respuesta>(`${this.baseUrl}/${id}/respuestas`, body);
  }

  /** Solicita una sugerencia generada por el motor RAG para el caso indicado. */
  pedirSugerenciaIA(id: string): Observable<Respuesta> {
    return this.http.post<Respuesta>(`${this.baseUrl}/${id}/sugerencia`, {});
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
