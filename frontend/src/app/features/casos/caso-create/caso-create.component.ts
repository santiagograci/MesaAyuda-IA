import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  EventEmitter,
  OnInit,
  Output,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  merge,
  of,
  switchMap
} from 'rxjs';
import {
  CasoDetalle,
  DeflexionResponse,
  PRIORIDAD_INFO,
  PRIORIDAD_LIST,
  PrioridadCaso,
  ProblemDetailsResponse
} from '../../../core/models/api.models';
import { CasosService } from '../../../core/services/casos.service';
import { SafeMarkdownPipe } from '../../../shared/pipes/safe-markdown.pipe';
import {
  readImageAsDataUrl,
  validateImageAttachment
} from '../../../shared/utils/image-attachment.util';

/**
 * Formulario reactivo para registrar un nuevo caso de soporte ERP
 * con guía operativa de niveles de prioridad.
 */
@Component({
  selector: 'app-caso-create',
  standalone: true,
  imports: [ReactiveFormsModule, SafeMarkdownPipe],
  template: `
    <section class="create-card card-surface anim-fade-up">
      <header class="create-header">
        <div>
          <h2>Nuevo Caso de Soporte ERP</h2>
          <p>Describe el código de error, módulo afectado y pasos previos realizados.</p>
        </div>
        <button type="button" class="btn btn-sm" (click)="cancelled.emit()">Cancelar</button>
      </header>

      <form [formGroup]="form" (ngSubmit)="submit()" class="create-form">
        <div class="field">
          <label for="titulo">Título del incidente</label>
          <input
            id="titulo"
            type="text"
            class="form-control"
            formControlName="titulo"
            placeholder="Ej. Error FAC-402 al anular factura electrónica DIAN"
          />
          @if (form.controls.titulo.touched && form.controls.titulo.invalid) {
            <small class="field-error">El título debe tener entre 5 y 150 caracteres.</small>
          }
        </div>

        <div class="field">
          <label for="prioridad">Prioridad</label>
          <select id="prioridad" class="form-control" formControlName="prioridad">
            @for (p of prioridades; track p) {
              <option [value]="p">{{ p }} — {{ prioridadInfo[p].etiqueta }}</option>
            }
          </select>

          <div class="priority-guide-box" role="note" aria-label="Guía de prioridades">
            <div class="guide-active">
              <span class="badge" [class]="'badge badge-prioridad-' + form.controls.prioridad.value">
                {{ form.controls.prioridad.value }}
              </span>
              <span>{{ prioridadInfo[form.controls.prioridad.value].descripcion }}</span>
            </div>
            <ul class="guide-list">
              @for (p of prioridades; track p) {
                <li [class.current]="form.controls.prioridad.value === p">
                  <strong>{{ p }}:</strong> {{ prioridadInfo[p].descripcion }}
                </li>
              }
            </ul>
          </div>
        </div>

        <div class="field">
          <label for="descripcion">Descripción detallada</label>
          <textarea
            id="descripcion"
            rows="5"
            class="form-control"
            formControlName="descripcion"
            placeholder="Describe el flujo exacto, mensaje de error y número de documento si aplica..."
          ></textarea>
          @if (form.controls.descripcion.touched && form.controls.descripcion.invalid) {
            <small class="field-error">La descripción debe tener entre 15 y 2000 caracteres.</small>
          }
        </div>

        <div class="field">
          <label for="createImageInput">📎 Adjuntar captura (PNG/JPG)</label>
          <div class="attachment-picker">
            <input
              #createImageFileInput
              id="createImageInput"
              type="file"
              accept="image/png,image/jpeg,image/webp"
              class="form-control"
              data-testid="create-image-input"
              [disabled]="submitting()"
              (change)="onImageSelected($event)"
            />
            <small class="attachment-hint">Opcional · PNG, JPG o WebP (máx. 2 MB).</small>
          </div>
          @if (imagenError()) {
            <small class="field-error" data-testid="create-image-error">{{ imagenError() }}</small>
          }
          @if (imagenAdjunta(); as preview) {
            <div class="attachment-preview">
              <img [src]="preview" alt="Vista previa de captura adjunta" class="attachment-thumb" />
              <div class="attachment-meta">
                <span>{{ imagenNombre() || 'Captura adjunta' }}</span>
                <button
                  type="button"
                  class="btn btn-sm"
                  data-testid="remove-create-image-btn"
                  (click)="quitarImagen(createImageFileInput)"
                >
                  ✕ Quitar
                </button>
              </div>
            </div>
          }
        </div>

        @if (deflexionLoading()) {
          <div class="deflexion-loading" role="status">
            <span class="spinner"></span> Buscando guías oficiales en la base de conocimiento...
          </div>
        }

        @if (deflexion(); as guia) {
          @if (guia.tieneSugerencia) {
            <aside class="deflexion-card anim-fade-up" role="region" aria-label="Asistente preventivo RAG">
              <div class="deflexion-head">
                <strong>
                  💡 Antes de crear el ticket, revisa si esta guía oficial resuelve tu consulta:
                </strong>
              </div>
              <div class="deflexion-answer md-content" [innerHTML]="guia.respuestaSugerida | safeMarkdown"></div>

              @if (guia.fuentes.length > 0) {
                <ul class="deflexion-sources">
                  @for (f of guia.fuentes; track $index) {
                    <li>
                      <strong>{{ f.documento }}</strong> · {{ f.seccion }}
                    </li>
                  }
                </ul>
              }

              <div class="deflexion-actions">
                <button
                  type="button"
                  class="btn btn-soft btn-sm"
                  data-testid="deflect-ticket-btn"
                  (click)="confirmarDeflexion()"
                >
                  ✅ ¡Esto resolvió mi duda! (No crear ticket)
                </button>
              </div>
            </aside>
          }
        }

        @if (errorMsg()) {
          <div class="alert-error" role="alert">{{ errorMsg() }}</div>
        }

        <div class="actions">
          <button type="button" class="btn" (click)="cancelled.emit()">Volver</button>
          <button type="submit" class="btn btn-primary" [disabled]="submitting()">
            @if (submitting()) {
              <span class="spinner"></span> Creando caso...
            } @else {
              Registrar Caso
            }
          </button>
        </div>
      </form>
    </section>
  `,
  styles: [
    `
      .create-card {
        padding: 1.5rem;
      }
      .create-header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        gap: 1rem;
        margin-bottom: 1.25rem;
        h2 {
          margin: 0 0 0.25rem;
          font-size: 1.25rem;
        }
        p {
          margin: 0;
          color: var(--text-secondary);
          font-size: 0.88rem;
        }
      }
      .create-form {
        display: grid;
        gap: 1rem;
      }
      .field {
        display: grid;
        gap: 0.35rem;
        label {
          font-size: 0.85rem;
          font-weight: 600;
        }
      }
      .priority-guide-box {
        margin-top: 0.35rem;
        padding: 0.75rem 0.9rem;
        border-radius: calc(var(--radius-card) * 0.75);
        background: var(--bg-surface-alt);
        border: 1px solid var(--border-color);
        display: grid;
        gap: 0.5rem;
      }
      .guide-active {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex-wrap: wrap;
        font-size: 0.84rem;
        font-weight: 600;
      }
      .guide-list {
        margin: 0;
        padding-left: 1.1rem;
        display: grid;
        gap: 0.25rem;
        font-size: 0.78rem;
        color: var(--text-secondary);
        li.current {
          color: var(--text-primary);
        }
      }
      .field-error {
        color: var(--danger-text);
        font-size: 0.78rem;
      }
      .actions {
        display: flex;
        justify-content: flex-end;
        gap: 0.65rem;
      }
      .deflexion-loading {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        font-size: 0.82rem;
        color: var(--text-secondary);
      }
      .deflexion-card {
        padding: 1rem;
        border-radius: calc(var(--radius-card) * 0.8);
        background: var(--accent-soft);
        border: 1px solid var(--accent-primary);
        display: grid;
        gap: 0.65rem;
      }
      .deflexion-head {
        font-size: 0.88rem;
        color: var(--accent-text);
      }
      .deflexion-answer {
        margin: 0;
        font-size: 0.86rem;
        line-height: 1.5;
        white-space: pre-line;
      }
      .deflexion-sources {
        margin: 0;
        padding-left: 1.15rem;
        font-size: 0.78rem;
        color: var(--text-secondary);
        display: grid;
        gap: 0.2rem;
      }
      .deflexion-actions {
        display: flex;
        justify-content: flex-end;
      }
      .attachment-picker {
        display: grid;
        gap: 0.25rem;
      }
      .attachment-hint {
        font-size: 0.76rem;
        color: var(--text-secondary);
      }
      .attachment-preview {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.55rem 0.75rem;
        border-radius: calc(var(--radius-card) * 0.75);
        border: 1px solid var(--border-color);
        background: var(--bg-surface-alt);
      }
      .attachment-thumb {
        width: 72px;
        height: 54px;
        object-fit: cover;
        border-radius: 6px;
        border: 1px solid var(--border-color);
      }
      .attachment-meta {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 0.5rem;
        flex: 1;
        font-size: 0.8rem;
        color: var(--text-secondary);
      }
    `
  ]
})
export class CasoCreateComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly casosService = inject(CasosService);
  private readonly destroyRef = inject(DestroyRef);

  @Output() created = new EventEmitter<CasoDetalle>();
  @Output() cancelled = new EventEmitter<void>();
  @Output() deflected = new EventEmitter<void>();

  readonly prioridades = PRIORIDAD_LIST;
  readonly prioridadInfo = PRIORIDAD_INFO;
  readonly submitting = signal<boolean>(false);
  readonly errorMsg = signal<string>('');
  readonly deflexionLoading = signal<boolean>(false);
  readonly deflexion = signal<DeflexionResponse | null>(null);
  readonly imagenAdjunta = signal<string | null>(null);
  readonly imagenNombre = signal<string>('');
  readonly imagenError = signal<string>('');

  readonly form = this.fb.nonNullable.group({
    titulo: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(150)]],
    prioridad: ['Media' as PrioridadCaso, [Validators.required]],
    descripcion: ['', [Validators.required, Validators.minLength(15), Validators.maxLength(2000)]]
  });

  ngOnInit(): void {
    merge(this.form.controls.titulo.valueChanges, this.form.controls.descripcion.valueChanges)
      .pipe(
        map(() => {
          const { titulo, descripcion } = this.form.getRawValue();
          return `${titulo.trim()}|||${descripcion.trim()}`;
        }),
        debounceTime(700),
        distinctUntilChanged(),
        switchMap(() => {
          const { titulo, descripcion } = this.form.getRawValue();
          const combined = `${titulo.trim()} ${descripcion.trim()}`.trim();
          if (combined.length < 15) {
            this.deflexionLoading.set(false);
            this.deflexion.set(null);
            return of(null);
          }
          this.deflexionLoading.set(true);
          return this.casosService.consultarDeflexion(titulo.trim(), descripcion.trim()).pipe(
            catchError(() => of(null))
          );
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((res) => {
        this.deflexionLoading.set(false);
        if (res && res.tieneSugerencia) {
          this.deflexion.set(res);
        } else if (res !== null) {
          this.deflexion.set(null);
        }
      });
  }

  confirmarDeflexion(): void {
    this.form.reset(
      {
        titulo: '',
        prioridad: 'Media',
        descripcion: ''
      },
      { emitEvent: false }
    );
    this.deflexion.set(null);
    this.imagenAdjunta.set(null);
    this.imagenNombre.set('');
    this.imagenError.set('');
    this.deflected.emit();
  }

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.imagenError.set('');

    if (!file) {
      this.imagenAdjunta.set(null);
      this.imagenNombre.set('');
      return;
    }

    const validationError = validateImageAttachment(file);
    if (validationError) {
      this.imagenAdjunta.set(null);
      this.imagenNombre.set('');
      this.imagenError.set(validationError);
      input.value = '';
      return;
    }

    void readImageAsDataUrl(file)
      .then((dataUrl) => {
        this.imagenAdjunta.set(dataUrl);
        this.imagenNombre.set(file.name);
      })
      .catch((err: unknown) => {
        this.imagenAdjunta.set(null);
        this.imagenNombre.set('');
        this.imagenError.set(err instanceof Error ? err.message : 'Error al leer la imagen.');
      });
  }

  quitarImagen(inputEl?: HTMLInputElement): void {
    this.imagenAdjunta.set(null);
    this.imagenNombre.set('');
    this.imagenError.set('');
    if (inputEl) {
      inputEl.value = '';
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMsg.set('');

    const raw = this.form.getRawValue();
    const img = this.imagenAdjunta();
    const payload = img ? { ...raw, imagenAdjuntaBase64: img } : raw;

    this.casosService.crearCaso(payload).subscribe({
      next: (detalle) => {
        this.submitting.set(false);
        this.created.emit(detalle);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.errorMsg.set(this.extractErrorMessage(err));
      }
    });
  }

  private extractErrorMessage(err: unknown): string {
    if (err instanceof HttpErrorResponse && err.error) {
      const problem = err.error as ProblemDetailsResponse;
      if (problem.errors) {
        const firstKey = Object.keys(problem.errors)[0];
        if (firstKey && problem.errors[firstKey]?.length) {
          return problem.errors[firstKey][0];
        }
      }
      return problem.detail || problem.title || 'No se pudo registrar el caso.';
    }
    return 'Error al comunicarse con el backend.';
  }
}
