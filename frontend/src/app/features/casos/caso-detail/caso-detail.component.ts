import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  EventEmitter,
  Input,
  OnDestroy,
  Output,
  computed,
  inject,
  signal
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';
import {
  CasoDetalle,
  EstadoCaso,
  FuenteCitada,
  PRIORIDAD_INFO,
  PRIORIDAD_LIST,
  PrioridadCaso,
  ProblemDetailsResponse,
  Respuesta
} from '../../../core/models/api.models';
import { AuthService } from '../../../core/services/auth.service';
import { CasosService } from '../../../core/services/casos.service';

const PRIORIDAD_RANK: Record<PrioridadCaso, number> = {
  Baja: 1,
  Media: 2,
  Alta: 3,
  Critica: 4
};

/**
 * Vista de detalle del caso con señal reactiva, máquina de estados,
 * borradores IA pendientes de aprobación del Analista, historial publicado y citas RAG.
 */
@Component({
  selector: 'app-caso-detail',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe],
  template: `
    @if (casoSignal(); as c) {
      <section class="detail-card card-surface">
        <header class="detail-header">
          <div class="title-block">
            <div class="badges-row">
              <span class="badge" [class]="'badge badge-estado-' + c.estado">
                {{ formatEstado(c.estado) }}
              </span>
              <span
                class="badge"
                [class]="'badge badge-prioridad-' + c.prioridad"
                [title]="prioridadInfo().descripcion"
              >
                Prioridad {{ c.prioridad }}
              </span>
              @if (canChangePriority()) {
                <button
                  type="button"
                  class="btn btn-soft btn-xs"
                  data-testid="toggle-priority-btn"
                  [disabled]="busy()"
                  (click)="abrirPanelPrioridad()"
                >
                  {{ authService.isAnalista() ? '🔺 Ajustar prioridad' : '🔺 Elevar severidad' }}
                </button>
              }
              <span class="meta-text">
                {{ c.clienteNombre }} · {{ c.fechaCreacion | date: 'dd/MM/yyyy HH:mm' }}
              </span>
            </div>
            <h2>{{ c.titulo }}</h2>
            <p class="priority-guide">
              <strong>Criterio {{ c.prioridad }}:</strong> {{ prioridadInfo().descripcion }}
            </p>
          </div>

          <div class="header-actions">
            @if (c.estado !== 'Cerrado') {
              <button
                type="button"
                class="btn btn-soft btn-sm"
                [disabled]="generatingIa() || busy()"
                (click)="pedirSugerencia()"
              >
                @if (generatingIa()) {
                  <span class="spinner"></span> Consultando RAG...
                } @else {
                  ✨ Pedir sugerencia a la IA
                }
              </button>
            }

            @if (authService.isAnalista()) {
              <button
                type="button"
                class="btn btn-danger btn-sm"
                [disabled]="busy() || deleteConfirmOpen()"
                (click)="iniciarEliminacion()"
              >
                Eliminar
              </button>
            }
          </div>
        </header>

        @if (priorityPanelOpen()) {
          <form
            [formGroup]="priorityForm"
            (ngSubmit)="confirmarCambioPrioridad()"
            class="priority-panel"
            role="region"
            aria-label="Cambio de prioridad"
          >
            <div class="priority-panel-head">
              <strong>
                {{ authService.isAnalista() ? 'Ajustar prioridad del caso' : 'Elevar severidad del caso' }}
              </strong>
              <span class="meta-text">
                Prioridad actual: <strong>{{ c.prioridad }}</strong>
              </span>
            </div>

            <div class="priority-field">
              <label for="nuevaPrioridad">Nueva prioridad</label>
              <select
                id="nuevaPrioridad"
                class="form-control"
                formControlName="nuevaPrioridad"
                (change)="onPrioritySelectChange($event)"
              >
                @for (p of availablePriorities(); track p) {
                  <option [value]="p">
                    {{ p }} — {{ prioridadInfoMap[p].etiqueta }}
                  </option>
                }
              </select>
              <small class="priority-help">{{ selectedPriorityGuide().descripcion }}</small>
            </div>

            <div class="priority-field">
              <label for="justificacionPrioridad">
                Motivo / impacto operativo del cambio de prioridad (mín. 10 caracteres)
              </label>
              <textarea
                id="justificacionPrioridad"
                rows="3"
                class="form-control"
                formControlName="justificacion"
                placeholder="Describe el impacto operativo o razón técnica que justifica el cambio..."
              ></textarea>
              @if (
                priorityForm.controls.justificacion.touched &&
                priorityForm.controls.justificacion.invalid
              ) {
                <small class="field-error">
                  La justificación es obligatoria y debe tener al menos 10 caracteres.
                </small>
              }
            </div>

            <div class="priority-panel-actions">
              <button
                type="submit"
                class="btn btn-primary btn-sm"
                data-testid="confirm-priority-btn"
                [disabled]="busy()"
              >
                Confirmar cambio
              </button>
              <button
                type="button"
                class="btn btn-sm"
                [disabled]="busy()"
                (click)="cerrarPanelPrioridad()"
              >
                Cancelar
              </button>
            </div>
          </form>
        }

        @if (deleteConfirmOpen()) {
          <div class="delete-confirm-banner" role="alertdialog" aria-label="Confirmar eliminación lógica">
            <p class="delete-confirm-text">
              ⚠️ ¿Estás seguro de eliminar este caso? Pasará a la papelera de reciclaje.
            </p>
            <div class="delete-confirm-actions">
              <button
                type="button"
                class="btn btn-danger btn-sm"
                data-testid="confirm-delete-btn"
                [disabled]="busy() || countdownSeconds() > 0"
                (click)="confirmarEliminacion()"
              >
                @if (countdownSeconds() > 0) {
                  Confirmar Eliminación ({{ countdownSeconds() }}s)
                } @else {
                  Sí, eliminar caso
                }
              </button>
              <button
                type="button"
                class="btn btn-sm"
                [disabled]="busy()"
                (click)="cancelarEliminacion()"
              >
                Cancelar
              </button>
            </div>
          </div>
        }

        <div class="description-box">
          <p>{{ c.descripcion }}</p>
        </div>

        @if (errorMsg()) {
          <div class="alert-error" role="alert">{{ errorMsg() }}</div>
        }
        @if (infoMsg()) {
          <div class="alert-info" role="status">{{ infoMsg() }}</div>
        }

        @if (allowedNextStates().length > 0) {
          <div class="workflow-bar">
            <span class="workflow-label">Flujo de estado:</span>
            @for (nextState of allowedNextStates(); track nextState) {
              <button
                type="button"
                class="btn btn-sm"
                [class.btn-primary]="nextState === 'Cerrado' || nextState === 'Resuelto'"
                [disabled]="busy()"
                (click)="cambiarEstado(nextState)"
              >
                Pasar a {{ formatEstado(nextState) }}
              </button>
            }
          </div>
        } @else {
          <div class="closed-banner">
            Este caso se encuentra <strong>Cerrado</strong> de forma definitiva y no admite cambios ni nuevas respuestas.
          </div>
        }

        @if (generatingIa() && authService.isAnalista()) {
          <section class="streaming-panel ia-card" aria-label="Generación de sugerencia IA en tiempo real">
            <div class="resp-header">
              <div class="resp-author">
                <span class="spinner"></span>
                <strong>Generando respuesta anclada a manuales ERP...</strong>
              </div>
              @if (streamingTokens() > 0) {
                <span class="badge badge-ia">{{ streamingTokens() }} tokens</span>
              }
            </div>

            @if (streamingSources().length > 0) {
              <div class="citations-box">
                <div class="citations-title">Fuentes citadas del manual Ofima ERP:</div>
                <div class="citations-grid">
                  @for (fuente of streamingSources(); track $index) {
                    <div class="citation-item">
                      <div class="citation-head">
                        <strong>{{ fuente.documento }}</strong>
                        <span class="score-pill">{{ fuente.puntaje * 100 | number: '1.0-1' }}%</span>
                      </div>
                      <div class="citation-sec">{{ fuente.seccion }}</div>
                      <div class="citation-frag">{{ fuente.fragmento }}</div>
                    </div>
                  }
                </div>
              </div>
            }

            <div class="streaming-output">
              <span>{{ streamingText() || 'Consultando base documental e iniciando redacción...' }}</span>
              <span class="stream-cursor" aria-hidden="true">▌</span>
            </div>
          </section>
        }

        @if (authService.isAnalista() && pendingIaDrafts().length > 0) {
          <section class="pending-drafts-section" aria-label="Borradores de IA pendientes de aprobación">
            <div class="section-heading">
              <h3>✨ Borradores de IA pendientes de aprobación ({{ pendingIaDrafts().length }})</h3>
              <span class="meta-text">El cliente aún no ve estos borradores hasta que los apruebes.</span>
            </div>

            <div class="responses-list">
              @for (draft of pendingIaDrafts(); track draft.id) {
                <article class="ia-card">
                  <div class="resp-header">
                    <div class="resp-author">
                      <strong>{{ draft.autorNombre }}</strong>
                      <span class="badge badge-ia">✨ Borrador IA (Pendiente revisión)</span>
                    </div>
                    <span class="meta-text">{{ draft.fechaCreacion | date: 'dd/MM/yyyy HH:mm' }}</span>
                  </div>

                  <div class="approval-box">
                    <label class="edit-label" [for]="'edit-' + draft.id">
                      Revisar y editar antes de publicar al cliente:
                    </label>
                    <textarea
                      [id]="'edit-' + draft.id"
                      rows="5"
                      class="form-control"
                      [disabled]="c.estado === 'Cerrado' || busy()"
                      [value]="getDraftText(draft)"
                      (input)="onDraftInput(draft.id, $event)"
                    ></textarea>
                    <div class="approval-actions">
                      <button
                        type="button"
                        class="btn btn-primary btn-sm"
                        [disabled]="busy() || c.estado === 'Cerrado'"
                        (click)="aprobarSugerencia(draft)"
                      >
                        Aprobar y Publicar al Cliente
                      </button>
                    </div>
                  </div>

                  @if (draft.fuentesCitadas.length > 0) {
                    <div class="citations-box">
                      <div class="citations-title">Fuentes citadas del manual Ofima ERP:</div>
                      <div class="citations-grid">
                        @for (fuente of draft.fuentesCitadas; track $index) {
                          <div class="citation-item">
                            <div class="citation-head">
                              <strong>{{ fuente.documento }}</strong>
                              <span class="score-pill">{{ fuente.puntaje * 100 | number: '1.0-1' }}%</span>
                            </div>
                            <div class="citation-sec">{{ fuente.seccion }}</div>
                            <div class="citation-frag">{{ fuente.fragmento }}</div>
                          </div>
                        }
                      </div>
                    </div>
                  }
                </article>
              }
            </div>
          </section>
        }

        <section class="history-section">
          <h3>Historial de Atención ({{ publishedRespuestas().length }})</h3>

          @if (publishedRespuestas().length === 0) {
            <div class="empty-history">
              Aún no hay respuestas publicadas. Puedes responder directamente o solicitar una sugerencia al motor RAG.
            </div>
          }

          <div class="responses-list">
            @for (resp of publishedRespuestas(); track resp.id) {
              <article [class]="resp.esGeneradaPorIA ? 'ia-card' : 'human-card'">
                <div class="resp-header">
                  <div class="resp-author">
                    <strong>{{ resp.autorNombre }}</strong>
                    @if (resp.esGeneradaPorIA) {
                      <span class="badge badge-ia">✨ IA Aprobada</span>
                    }
                  </div>
                  <span class="meta-text">{{ resp.fechaCreacion | date: 'dd/MM/yyyy HH:mm' }}</span>
                </div>

                <p class="resp-body">{{ resp.texto }}</p>

                @if (resp.fuentesCitadas.length > 0) {
                  <div class="citations-box">
                    <div class="citations-title">Fuentes citadas del manual Ofima ERP:</div>
                    <div class="citations-grid">
                      @for (fuente of resp.fuentesCitadas; track $index) {
                        <div class="citation-item">
                          <div class="citation-head">
                            <strong>{{ fuente.documento }}</strong>
                            <span class="score-pill">{{ fuente.puntaje * 100 | number: '1.0-1' }}%</span>
                          </div>
                          <div class="citation-sec">{{ fuente.seccion }}</div>
                          <div class="citation-frag">{{ fuente.fragmento }}</div>
                        </div>
                      }
                    </div>
                  </div>
                }
              </article>
            }
          </div>
        </section>

        @if (c.estado !== 'Cerrado') {
          <form [formGroup]="replyForm" (ngSubmit)="enviarRespuesta()" class="reply-form">
            <label for="replyText">Agregar respuesta humana</label>
            <textarea
              id="replyText"
              rows="3"
              class="form-control"
              formControlName="texto"
              placeholder="Escribe una respuesta o aclaración sobre el caso..."
            ></textarea>
            @if (replyForm.controls.texto.touched && replyForm.controls.texto.invalid) {
              <small class="field-error">La respuesta debe tener entre 5 y 4000 caracteres.</small>
            }
            <div class="reply-actions">
              <button type="submit" class="btn btn-primary" [disabled]="busy()">
                Enviar Respuesta
              </button>
            </div>
          </form>
        }
      </section>
    }
  `,
  styles: [
    `
      .detail-card {
        padding: 1.5rem;
        display: grid;
        gap: 1.15rem;
        align-content: start;
      }
      .detail-header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        flex-wrap: wrap;
        gap: 1rem;
      }
      .title-block {
        display: grid;
        gap: 0.4rem;
        h2 {
          margin: 0;
          font-size: 1.25rem;
        }
      }
      .priority-guide {
        margin: 0;
        font-size: 0.8rem;
        color: var(--text-secondary);
      }
      .badges-row {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 0.5rem;
      }
      .meta-text {
        font-size: 0.78rem;
        color: var(--text-secondary);
      }
      .header-actions {
        display: flex;
        gap: 0.5rem;
        flex-wrap: wrap;
      }
      .description-box {
        padding: 0.9rem 1rem;
        border-radius: calc(var(--radius-card) * 0.8);
        background: var(--bg-surface-alt);
        border: 1px solid var(--border-color);
        p {
          margin: 0;
          white-space: pre-wrap;
        }
      }
      .workflow-bar {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 0.5rem;
        padding: 0.75rem 1rem;
        border-radius: calc(var(--radius-card) * 0.8);
        border: 1px dashed var(--border-color);
      }
      .closed-banner {
        padding: 0.75rem 1rem;
        border-radius: calc(var(--radius-card) * 0.8);
        background: var(--bg-surface-alt);
        border: 1px solid var(--border-color);
        color: var(--text-secondary);
        font-size: 0.84rem;
      }
      .workflow-label {
        font-size: 0.82rem;
        font-weight: 600;
        color: var(--text-secondary);
        margin-right: 0.25rem;
      }
      .pending-drafts-section,
      .history-section {
        display: grid;
        gap: 0.8rem;
        h3 {
          margin: 0;
          font-size: 1rem;
        }
      }
      .section-heading {
        display: flex;
        justify-content: space-between;
        align-items: baseline;
        flex-wrap: wrap;
        gap: 0.5rem;
      }
      .empty-history {
        padding: 1rem;
        border: 1px dashed var(--border-color);
        border-radius: calc(var(--radius-card) * 0.8);
        color: var(--text-secondary);
        font-size: 0.86rem;
      }
      .responses-list {
        display: grid;
        gap: 0.85rem;
      }
      .human-card {
        padding: 1rem;
        border-radius: var(--radius-card);
        border: 1px solid var(--border-color);
        background: var(--bg-surface-alt);
      }
      .resp-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 0.5rem;
        margin-bottom: 0.55rem;
      }
      .resp-author {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex-wrap: wrap;
      }
      .resp-body {
        margin: 0;
        white-space: pre-wrap;
        font-size: 0.92rem;
      }
      .approval-box {
        display: grid;
        gap: 0.55rem;
      }
      .edit-label {
        font-size: 0.8rem;
        font-weight: 600;
        color: var(--accent-text);
      }
      .approval-actions,
      .reply-actions {
        display: flex;
        justify-content: flex-end;
      }
      .citations-box {
        margin-top: 0.85rem;
        padding-top: 0.75rem;
        border-top: 1px solid var(--border-color);
      }
      .citations-title {
        font-size: 0.78rem;
        font-weight: 700;
        color: var(--text-secondary);
        margin-bottom: 0.5rem;
      }
      .citations-grid {
        display: grid;
        gap: 0.5rem;
      }
      .citation-item {
        padding: 0.6rem 0.75rem;
        border-radius: calc(var(--radius-card) * 0.7);
        background: var(--bg-surface);
        border: 1px solid var(--border-color);
        font-size: 0.8rem;
      }
      .citation-head {
        display: flex;
        justify-content: space-between;
        gap: 0.5rem;
      }
      .score-pill {
        font-weight: 700;
        color: var(--accent-text);
      }
      .citation-sec {
        color: var(--accent-text);
        font-weight: 600;
        margin: 0.15rem 0;
      }
      .citation-frag {
        color: var(--text-secondary);
      }
      .reply-form {
        display: grid;
        gap: 0.5rem;
        padding-top: 0.75rem;
        border-top: 1px solid var(--border-color);
        label {
          font-size: 0.85rem;
          font-weight: 600;
        }
      }
      .field-error {
        color: var(--danger-text);
        font-size: 0.78rem;
      }
      .delete-confirm-banner {
        display: flex;
        flex-wrap: wrap;
        justify-content: space-between;
        align-items: center;
        gap: 0.75rem;
        padding: 0.85rem 1rem;
        border-radius: calc(var(--radius-card) * 0.75);
        border: 1px solid var(--danger-border, #f59e0b);
        background: var(--danger-soft, rgba(239, 68, 68, 0.1));
      }
      .delete-confirm-text {
        margin: 0;
        font-size: 0.86rem;
        font-weight: 600;
        color: var(--danger-text, #b91c1c);
      }
      .delete-confirm-actions {
        display: flex;
        gap: 0.5rem;
        align-items: center;
      }
      .btn-xs {
        padding: 0.22rem 0.55rem;
        font-size: 0.76rem;
      }
      .priority-panel {
        display: grid;
        gap: 0.75rem;
        padding: 1rem;
        border-radius: calc(var(--radius-card) * 0.8);
        border: 1px solid var(--border-color);
        background: var(--bg-surface-alt);
      }
      .priority-panel-head {
        display: flex;
        justify-content: space-between;
        align-items: center;
        flex-wrap: wrap;
        gap: 0.5rem;
      }
      .priority-field {
        display: grid;
        gap: 0.35rem;
        label {
          font-size: 0.84rem;
          font-weight: 600;
        }
      }
      .priority-help {
        font-size: 0.78rem;
        color: var(--text-secondary);
      }
      .priority-panel-actions {
        display: flex;
        gap: 0.5rem;
        flex-wrap: wrap;
      }
      .streaming-panel {
        border: 1px dashed var(--accent-primary);
      }
      .streaming-output {
        font-size: 0.92rem;
        line-height: 1.55;
        white-space: pre-wrap;
        color: var(--text-primary);
      }
      .stream-cursor {
        display: inline-block;
        margin-left: 2px;
        color: var(--accent-primary);
        animation: streamBlink 0.9s steps(2, start) infinite;
      }
      @keyframes streamBlink {
        to {
          visibility: hidden;
        }
      }
    `
  ]
})
export class CasoDetailComponent implements OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly casosService = inject(CasosService);
  readonly authService = inject(AuthService);

  readonly casoSignal = signal<CasoDetalle | null>(null);

  @Input({ required: true })
  set caso(value: CasoDetalle) {
    const previousId = this.casoSignal()?.id;
    this.casoSignal.set(value);
    if (previousId !== value.id) {
      this.errorMsg.set('');
      this.infoMsg.set('');
      this.draftEdits.set({});
      this.replyForm.reset();
      this.cancelarEliminacion();
      this.cerrarPanelPrioridad();
      this.resetStreamingState();
    }
  }

  get caso(): CasoDetalle {
    return this.casoSignal()!;
  }

  @Output() casoActualizado = new EventEmitter<CasoDetalle>();
  @Output() casoEliminado = new EventEmitter<string>();

  readonly busy = signal<boolean>(false);
  readonly generatingIa = signal<boolean>(false);
  readonly errorMsg = signal<string>('');
  readonly infoMsg = signal<string>('');
  readonly draftEdits = signal<Record<string, string>>({});
  readonly deleteConfirmOpen = signal<boolean>(false);
  readonly countdownSeconds = signal<number>(3);
  readonly priorityPanelOpen = signal<boolean>(false);
  readonly selectedNewPriority = signal<PrioridadCaso>('Alta');
  readonly prioridadInfoMap = PRIORIDAD_INFO;
  readonly streamingText = signal<string>('');
  readonly streamingSources = signal<FuenteCitada[]>([]);
  readonly streamingTokens = signal<number>(0);

  private countdownTimer: ReturnType<typeof setInterval> | null = null;
  private streamSub: Subscription | null = null;

  ngOnDestroy(): void {
    this.clearCountdownTimer();
    this.resetStreamingState();
  }

  readonly availablePriorities = computed<PrioridadCaso[]>(() => {
    const current = this.casoSignal();
    if (!current) {
      return [];
    }
    if (this.authService.isAnalista()) {
      return PRIORIDAD_LIST.filter((p) => p !== current.prioridad);
    }
    const currentRank = PRIORIDAD_RANK[current.prioridad];
    return PRIORIDAD_LIST.filter((p) => PRIORIDAD_RANK[p] > currentRank);
  });

  readonly canChangePriority = computed<boolean>(() => {
    const current = this.casoSignal();
    return !!current && current.estado !== 'Cerrado' && this.availablePriorities().length > 0;
  });

  readonly selectedPriorityGuide = computed(() => PRIORIDAD_INFO[this.selectedNewPriority()]);

  readonly priorityForm = this.fb.nonNullable.group({
    nuevaPrioridad: ['Alta' as PrioridadCaso, [Validators.required]],
    justificacion: [
      '',
      [Validators.required, Validators.minLength(10), Validators.maxLength(1000)]
    ]
  });

  readonly prioridadInfo = computed(() => {
    const prioridad = this.casoSignal()?.prioridad ?? 'Media';
    return PRIORIDAD_INFO[prioridad];
  });

  readonly allowedNextStates = computed<EstadoCaso[]>(() => {
    switch (this.casoSignal()?.estado) {
      case 'Abierto':
        return ['EnProceso'];
      case 'EnProceso':
        return ['Resuelto'];
      case 'Resuelto':
        return ['EnProceso', 'Cerrado'];
      default:
        return [];
    }
  });

  readonly pendingIaDrafts = computed<Respuesta[]>(() => {
    const respuestas = this.casoSignal()?.respuestas ?? [];
    return respuestas.filter((r) => r.esGeneradaPorIA && !r.aprobadaPorAnalista);
  });

  readonly publishedRespuestas = computed<Respuesta[]>(() => {
    const respuestas = this.casoSignal()?.respuestas ?? [];
    return respuestas.filter((r) => !r.esGeneradaPorIA || r.aprobadaPorAnalista);
  });

  readonly replyForm = this.fb.nonNullable.group({
    texto: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(4000)]]
  });

  formatEstado(estado: EstadoCaso): string {
    return estado === 'EnProceso' ? 'En Proceso' : estado;
  }

  getDraftText(resp: Respuesta): string {
    return this.draftEdits()[resp.id] ?? resp.texto;
  }

  onDraftInput(respId: string, event: Event): void {
    const value = (event.target as HTMLTextAreaElement).value;
    this.draftEdits.update((prev) => ({ ...prev, [respId]: value }));
  }

  pedirSugerencia(): void {
    const current = this.casoSignal();
    if (!current) {
      return;
    }

    this.resetStreamingState();
    this.generatingIa.set(true);
    this.errorMsg.set('');
    this.infoMsg.set('');

    if (this.authService.isAnalista()) {
      this.streamSub = this.casosService.sugerirRespuestaStream(current.id).subscribe({
        next: (event) => {
          if (event.type === 'sources') {
            this.streamingSources.set(event.fuentes);
            this.streamingTokens.set(event.tokensUsados);
          } else if (event.type === 'token') {
            this.streamingText.update((prev) => prev + event.chunk);
          } else if (event.type === 'done') {
            this.generatingIa.set(false);
            this.streamingText.set('');
            this.streamingSources.set([]);
            this.streamingTokens.set(0);
            this.casoSignal.set(event.caso);
            this.infoMsg.set(
              'Sugerencia IA generada en vivo. Revísala en la sección de borradores y publícala cuando esté lista.'
            );
            this.casoActualizado.emit(event.caso);
          } else if (event.type === 'error') {
            this.generatingIa.set(false);
            this.errorMsg.set(event.detail);
          }
        },
        error: (err: unknown) => {
          this.generatingIa.set(false);
          this.errorMsg.set(
            err instanceof Error ? err.message : this.extractErrorMessage(err)
          );
        },
        complete: () => {
          this.generatingIa.set(false);
          this.streamSub = null;
        }
      });
      return;
    }

    this.casosService.pedirSugerenciaIA(current.id).subscribe({
      next: () => {
        this.generatingIa.set(false);
        this.infoMsg.set('Sugerencia IA generada y enviada a revisión del Analista.');
        this.reloadCaso(current.id);
      },
      error: (err: unknown) => {
        this.generatingIa.set(false);
        this.errorMsg.set(this.extractErrorMessage(err));
      }
    });
  }

  private resetStreamingState(): void {
    if (this.streamSub) {
      this.streamSub.unsubscribe();
      this.streamSub = null;
    }
    this.generatingIa.set(false);
    this.streamingText.set('');
    this.streamingSources.set([]);
    this.streamingTokens.set(0);
  }

  aprobarSugerencia(resp: Respuesta): void {
    const current = this.casoSignal();
    if (!current) {
      return;
    }

    const textoFinal = this.getDraftText(resp).trim();
    if (textoFinal.length < 5) {
      this.errorMsg.set('El texto aprobado debe tener al menos 5 caracteres.');
      return;
    }

    this.busy.set(true);
    this.errorMsg.set('');
    this.infoMsg.set('');

    this.casosService.aprobarSugerenciaIA(current.id, resp.id, textoFinal).subscribe({
      next: () => {
        this.busy.set(false);
        this.draftEdits.update((prev) => {
          const next = { ...prev };
          delete next[resp.id];
          return next;
        });
        this.infoMsg.set('Respuesta IA aprobada y publicada al cliente.');
        this.reloadCaso(current.id);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.errorMsg.set(this.extractErrorMessage(err));
      }
    });
  }

  cambiarEstado(nuevoEstado: EstadoCaso): void {
    const current = this.casoSignal();
    if (!current) {
      return;
    }

    this.busy.set(true);
    this.errorMsg.set('');
    this.infoMsg.set('');

    this.casosService.cambiarEstado(current.id, nuevoEstado).subscribe({
      next: (actualizado) => {
        this.busy.set(false);
        this.casoSignal.set(actualizado);
        this.infoMsg.set(`Estado actualizado a ${this.formatEstado(actualizado.estado)}.`);
        this.casoActualizado.emit(actualizado);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.errorMsg.set(this.extractErrorMessage(err));
      }
    });
  }

  enviarRespuesta(): void {
    const current = this.casoSignal();
    if (!current) {
      return;
    }

    if (this.replyForm.invalid) {
      this.replyForm.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.errorMsg.set('');
    this.infoMsg.set('');

    this.casosService.agregarRespuesta(current.id, this.replyForm.getRawValue().texto).subscribe({
      next: () => {
        this.busy.set(false);
        this.replyForm.reset();
        this.infoMsg.set('Respuesta publicada en el historial del caso.');
        this.reloadCaso(current.id);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.errorMsg.set(this.extractErrorMessage(err));
      }
    });
  }

  abrirPanelPrioridad(): void {
    const options = this.availablePriorities();
    if (options.length === 0) {
      return;
    }

    const defaultTarget = options[0];
    this.selectedNewPriority.set(defaultTarget);
    this.priorityForm.reset({
      nuevaPrioridad: defaultTarget,
      justificacion: ''
    });
    this.errorMsg.set('');
    this.infoMsg.set('');
    this.priorityPanelOpen.set(true);
  }

  cerrarPanelPrioridad(): void {
    this.priorityPanelOpen.set(false);
    this.priorityForm.reset({
      nuevaPrioridad: 'Alta',
      justificacion: ''
    });
  }

  onPrioritySelectChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as PrioridadCaso;
    this.selectedNewPriority.set(value);
  }

  confirmarCambioPrioridad(): void {
    const current = this.casoSignal();
    if (!current) {
      return;
    }

    if (this.priorityForm.invalid) {
      this.priorityForm.markAllAsTouched();
      return;
    }

    const { nuevaPrioridad, justificacion } = this.priorityForm.getRawValue();
    const justificacionTrim = justificacion.trim();
    if (justificacionTrim.length < 10) {
      this.priorityForm.controls.justificacion.setErrors({ minlength: true });
      this.priorityForm.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.errorMsg.set('');
    this.infoMsg.set('');

    this.casosService
      .cambiarPrioridad(current.id, nuevaPrioridad, justificacionTrim)
      .subscribe({
        next: (actualizado) => {
          this.busy.set(false);
          this.casoSignal.set(actualizado);
          this.cerrarPanelPrioridad();
          this.infoMsg.set(`Prioridad actualizada a ${actualizado.prioridad}.`);
          this.casoActualizado.emit(actualizado);
        },
        error: (err: unknown) => {
          this.busy.set(false);
          this.errorMsg.set(this.extractErrorMessage(err));
        }
      });
  }

  iniciarEliminacion(): void {
    this.clearCountdownTimer();
    this.deleteConfirmOpen.set(true);
    this.countdownSeconds.set(3);

    this.countdownTimer = setInterval(() => {
      const remaining = this.countdownSeconds();
      if (remaining <= 1) {
        this.countdownSeconds.set(0);
        this.clearCountdownTimer();
      } else {
        this.countdownSeconds.set(remaining - 1);
      }
    }, 1000);
  }

  cancelarEliminacion(): void {
    this.clearCountdownTimer();
    this.deleteConfirmOpen.set(false);
    this.countdownSeconds.set(3);
  }

  confirmarEliminacion(): void {
    const current = this.casoSignal();
    if (!current || this.countdownSeconds() > 0) {
      return;
    }

    this.busy.set(true);
    this.errorMsg.set('');

    this.casosService.eliminarCaso(current.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.cancelarEliminacion();
        this.casoEliminado.emit(current.id);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.errorMsg.set(this.extractErrorMessage(err));
      }
    });
  }

  private clearCountdownTimer(): void {
    if (this.countdownTimer !== null) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  }

  private reloadCaso(casoId: string): void {
    this.casosService.obtenerCaso(casoId).subscribe({
      next: (actualizado) => {
        this.casoSignal.set(actualizado);
        this.casoActualizado.emit(actualizado);
      },
      error: (err: unknown) => {
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
      return problem.detail || problem.title || 'No se pudo completar la operación.';
    }
    return 'Error de comunicación con el servidor.';
  }
}
