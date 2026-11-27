import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, OnInit, Output, inject, signal } from '@angular/core';
import {
  DocumentosRagResponse,
  IngestaArchivoResponse,
  ProblemDetailsResponse
} from '../../../core/models/api.models';
import { DocumentosService } from '../../../core/services/documentos.service';

const MAX_UPLOAD_BYTES = 5 * 1024 * 1024;
const ALLOWED_EXTENSIONS = ['.md', '.txt', '.pdf'];

/**
 * Modal de administración en vivo de la Base de Conocimiento RAG para Analistas.
 */
@Component({
  selector: 'app-knowledge-base-modal',
  standalone: true,
  template: `
    <div class="modal-backdrop" (click)="close.emit()">
      <section
        class="modal-card card-surface"
        role="dialog"
        aria-modal="true"
        aria-label="Gestor de Base de Conocimiento RAG"
        (click)="$event.stopPropagation()"
      >
        <header class="modal-header">
          <div>
            <h2>📚 Base de Conocimiento RAG</h2>
            <p class="muted">
              Manuales oficiales Ofima ERP indexados en ChromaDB + BM25 para sugerencias IA.
            </p>
          </div>
          <button
            type="button"
            class="btn btn-sm"
            aria-label="Cerrar modal"
            (click)="close.emit()"
          >
            ✕
          </button>
        </header>

        @if (errorMsg()) {
          <div class="alert-error" role="alert">{{ errorMsg() }}</div>
        }
        @if (uploadSuccess(); as res) {
          <div class="alert-info" role="status">
            ✅ <strong>{{ res.archivo }}</strong> indexado con
            <strong>{{ res.fragmentosArchivo }}</strong> fragmento(s). Base total:
            {{ res.totalDocumentos }} manuales / {{ res.totalFragmentos }} fragmentos.
          </div>
        }

        <form class="upload-box" (submit)="onSubmit($event)">
          <label for="kbFileInput" class="upload-label">
            Subir nuevo manual (<code>.md</code>, <code>.txt</code> o <code>.pdf</code>, máx. 5 MB)
          </label>
          <div class="upload-row">
            <input
              id="kbFileInput"
              type="file"
              accept=".md,.txt,.pdf"
              class="form-control"
              [disabled]="uploading()"
              (change)="onFileSelected($event)"
            />
            <button
              type="submit"
              class="btn btn-primary btn-sm"
              data-testid="upload-kb-btn"
              [disabled]="!selectedFile() || uploading()"
            >
              @if (uploading()) {
                <span class="spinner"></span> Indexando...
              } @else {
                Subir e Indexar
              }
            </button>
          </div>
        </form>

        <section class="inventory-section">
          <div class="inventory-head">
            <h3>Manuales indexados</h3>
            @if (inventario(); as inv) {
              <span class="badge badge-ia">
                {{ inv.totalDocumentos }} documentos · {{ inv.totalFragmentos }} fragmentos
              </span>
            }
          </div>

          @if (loading()) {
            <div class="empty-box"><span class="spinner"></span> Cargando inventario...</div>
          } @else {
            @if (inventario(); as inv) {
              @if (inv.documentos.length === 0) {
                <div class="empty-box">No hay documentos indexados en la base de conocimiento.</div>
              } @else {
                <ul class="doc-list">
                  @for (doc of inv.documentos; track doc.documento) {
                    <li class="doc-item">
                      <span class="doc-name">{{ doc.documento }}</span>
                      <span class="badge">{{ doc.fragmentos }} fragmentos</span>
                    </li>
                  }
                </ul>
              }
            }
          }
        </section>
      </section>
    </div>
  `,
  styles: [
    `
      .modal-backdrop {
        position: fixed;
        inset: 0;
        background: rgba(15, 23, 42, 0.55);
        backdrop-filter: blur(2px);
        z-index: 9500;
        display: grid;
        place-items: center;
        padding: 1rem;
      }
      .modal-card {
        width: 100%;
        max-width: 640px;
        max-height: 85vh;
        overflow-y: auto;
        padding: 1.4rem;
        display: grid;
        gap: 1rem;
      }
      .modal-header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        gap: 1rem;
        h2 {
          margin: 0 0 0.25rem;
          font-size: 1.15rem;
        }
        p {
          margin: 0;
          font-size: 0.84rem;
        }
      }
      .muted {
        color: var(--text-secondary);
      }
      .upload-box {
        display: grid;
        gap: 0.5rem;
        padding: 0.9rem;
        border-radius: calc(var(--radius-card) * 0.8);
        border: 1px dashed var(--border-color);
        background: var(--bg-surface-alt);
      }
      .upload-label {
        font-size: 0.84rem;
        font-weight: 600;
      }
      .upload-row {
        display: flex;
        flex-wrap: wrap;
        gap: 0.6rem;
        align-items: center;
        input[type='file'] {
          flex: 1;
          min-width: 220px;
        }
      }
      .inventory-section {
        display: grid;
        gap: 0.6rem;
      }
      .inventory-head {
        display: flex;
        justify-content: space-between;
        align-items: center;
        flex-wrap: wrap;
        gap: 0.5rem;
        h3 {
          margin: 0;
          font-size: 0.95rem;
        }
      }
      .doc-list {
        list-style: none;
        margin: 0;
        padding: 0;
        display: grid;
        gap: 0.45rem;
      }
      .doc-item {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 0.75rem;
        padding: 0.55rem 0.75rem;
        border-radius: calc(var(--radius-card) * 0.7);
        border: 1px solid var(--border-color);
        background: var(--bg-surface-alt);
        font-size: 0.86rem;
      }
      .doc-name {
        font-weight: 600;
        word-break: break-word;
      }
      .empty-box {
        padding: 1.2rem;
        text-align: center;
        color: var(--text-secondary);
        font-size: 0.86rem;
      }
    `
  ]
})
export class KnowledgeBaseModalComponent implements OnInit {
  private readonly documentosService = inject(DocumentosService);

  @Output() close = new EventEmitter<void>();

  readonly inventario = signal<DocumentosRagResponse | null>(null);
  readonly selectedFile = signal<File | null>(null);
  readonly loading = signal<boolean>(false);
  readonly uploading = signal<boolean>(false);
  readonly errorMsg = signal<string>('');
  readonly uploadSuccess = signal<IngestaArchivoResponse | null>(null);

  ngOnInit(): void {
    this.loadInventario();
  }

  onFileSelected(event: Event): void {
    this.errorMsg.set('');
    this.uploadSuccess.set(null);

    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    if (!file) {
      this.selectedFile.set(null);
      return;
    }

    const lower = file.name.toLowerCase();
    if (!ALLOWED_EXTENSIONS.some((ext) => lower.endsWith(ext))) {
      this.selectedFile.set(null);
      this.errorMsg.set('Formato no permitido. Selecciona un archivo .md, .txt o .pdf.');
      return;
    }

    if (file.size === 0) {
      this.selectedFile.set(null);
      this.errorMsg.set('El archivo seleccionado está vacío.');
      return;
    }

    if (file.size > MAX_UPLOAD_BYTES) {
      this.selectedFile.set(null);
      this.errorMsg.set('El archivo supera el límite permitido de 5 MB.');
      return;
    }

    this.selectedFile.set(file);
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    const file = this.selectedFile();
    if (!file) {
      return;
    }

    this.uploading.set(true);
    this.errorMsg.set('');
    this.uploadSuccess.set(null);

    this.documentosService.ingestarArchivo(file).subscribe({
      next: (res) => {
        this.uploading.set(false);
        this.selectedFile.set(null);
        this.uploadSuccess.set(res);
        this.loadInventario();
      },
      error: (err: unknown) => {
        this.uploading.set(false);
        this.errorMsg.set(this.extractError(err));
      }
    });
  }

  private loadInventario(): void {
    this.loading.set(true);
    this.documentosService.listarDocumentos().subscribe({
      next: (data) => {
        this.loading.set(false);
        this.inventario.set(data);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.errorMsg.set(this.extractError(err));
      }
    });
  }

  private extractError(err: unknown): string {
    if (err instanceof HttpErrorResponse && err.error) {
      const problem = err.error as ProblemDetailsResponse;
      return problem.detail || problem.title || 'No se pudo procesar la solicitud documental.';
    }
    return 'Error de comunicación con el servidor.';
  }
}
