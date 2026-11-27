import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DocumentosRagResponse, IngestaArchivoResponse } from '../models/api.models';

/**
 * Cliente HTTP para administrar la base de conocimiento RAG mediante /api/documentos.
 */
@Injectable({ providedIn: 'root' })
export class DocumentosService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/documentos';

  /** Obtiene la lista de manuales indexados y el total de fragmentos en ChromaDB. */
  listarDocumentos(): Observable<DocumentosRagResponse> {
    return this.http.get<DocumentosRagResponse>(this.baseUrl);
  }

  /** Sube un manual (.md, .txt o .pdf) para su indexación inmediata en el servicio RAG. */
  ingestarArchivo(file: File): Observable<IngestaArchivoResponse> {
    const formData = new FormData();
    formData.append('archivo', file, file.name);
    return this.http.post<IngestaArchivoResponse>(`${this.baseUrl}/ingestar`, formData);
  }
}
