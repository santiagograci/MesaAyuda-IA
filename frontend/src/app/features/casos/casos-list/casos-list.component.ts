import { DatePipe } from '@angular/common';
import {
  Component,
  DestroyRef,
  EventEmitter,
  Input,
  OnInit,
  Output,
  inject
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import {
  CasoEliminado,
  CasoResumen,
  EstadoCaso,
  EstadoSla,
  PrioridadCaso
} from '../../../core/models/api.models';

/**
 * Columna izquierda del Split-Screen: filtros por estado, listado paginado,
 * papelera de reciclaje y restauración de datos demo.
 */
@Component({
  selector: 'app-casos-list',
  standalone: true,
  imports: [DatePipe],
  template: `
    <aside class="list-panel card-surface anim-fade-up">
      <div class="list-header">
        <div class="list-title-row">
          <div>
            <h2>{{ papeleraMode ? 'Papelera de Reciclaje' : 'Casos de Soporte' }}</h2>
            <span class="muted">
              {{ papeleraMode ? papeleraCasos.length + ' en papelera' : totalCount + ' registros' }}
            </span>
          </div>

          @if (isAnalista) {
            <div class="mode-tabs" role="tablist" aria-label="Modo de bandeja">
              <button
                type="button"
                class="filter-pill"
                [class.active]="!papeleraMode"
                (click)="togglePapelera.emit(false)"
              >
                Bandeja
              </button>
              <button
                type="button"
                class="filter-pill trash-tab"
                [class.active]="papeleraMode"
                (click)="togglePapelera.emit(true)"
              >
                🗑️ Papelera ({{ papeleraCount }})
              </button>
            </div>
          }
        </div>
      </div>

      @if (!papeleraMode) {
        <div class="search-toolbar">
          <input
            type="search"
            class="form-control search-input"
            data-testid="cases-search-input"
            [value]="busqueda"
            (input)="onSearchInput($event)"
            placeholder="Buscar por código (ej. CON-301), título o cliente..."
            aria-label="Buscar casos"
          />
          <select
            class="form-control priority-select"
            data-testid="priority-filter-select"
            [value]="prioridadFilter"
            (change)="onPriorityChange($event)"
            aria-label="Filtrar por prioridad"
          >
            <option value="">Todas las prioridades</option>
            <option value="Critica">Crítica</option>
            <option value="Alta">Alta</option>
            <option value="Media">Media</option>
            <option value="Baja">Baja</option>
          </select>
        </div>

        <div class="filter-bar" role="tablist" aria-label="Filtrar por estado">
          @for (opt of filterOptions; track opt.value) {
            <button
              type="button"
              class="filter-pill"
              [class.active]="estadoFilter === opt.value"
              (click)="estadoFilterChange.emit(opt.value)"
            >
              {{ opt.label }}
            </button>
          }
        </div>
      }

      <div class="cases-scroll">
        @if (loading) {
          <div class="empty-box"><span class="spinner"></span> Cargando casos...</div>
        } @else if (papeleraMode) {
          @if (papeleraCasos.length === 0) {
            <div class="empty-box">
              <p>La papelera de reciclaje está vacía.</p>
              @if (isAnalista) {
                <button
                  type="button"
                  class="btn btn-soft btn-sm demo-btn"
                  [disabled]="restoringDemo"
                  (click)="restaurarDemo.emit()"
                >
                  @if (restoringDemo) {
                    <span class="spinner"></span> Restaurando...
                  } @else {
                    🔄 Restaurar Casos Demo
                  }
                </button>
              }
            </div>
          } @else {
            @for (item of papeleraCasos; track item.id) {
              <div class="case-item trash-item">
                <div class="case-top">
                  <span class="badge" [class]="'badge badge-estado-' + item.estado">
                    {{ formatEstado(item.estado) }}
                  </span>
                  <span class="badge" [class]="'badge badge-prioridad-' + item.prioridad">
                    {{ item.prioridad }}
                  </span>
                </div>

                <div class="case-title">{{ item.titulo }}</div>

                <div class="case-meta">
                  <span>{{ item.clienteNombre }}</span>
                  @if (item.fechaEliminacion) {
                    <span>Eliminado {{ item.fechaEliminacion | date: 'dd/MM HH:mm' }}</span>
                  }
                </div>

                <div class="trash-actions">
                  <button
                    type="button"
                    class="btn btn-soft btn-sm"
                    [disabled]="restoringId === item.id"
                    (click)="restaurarCaso.emit(item.id)"
                  >
                    @if (restoringId === item.id) {
                      <span class="spinner"></span> Restaurando...
                    } @else {
                      ♻️ Restaurar caso
                    }
                  </button>
                </div>
              </div>
            }
          }
        } @else if (casos.length === 0) {
          <div class="empty-box">
            <p>No hay casos registrados con el filtro actual.</p>
            @if (isAnalista && estadoFilter === '') {
              <button
                type="button"
                class="btn btn-soft btn-sm demo-btn"
                [disabled]="restoringDemo"
                (click)="restaurarDemo.emit()"
              >
                @if (restoringDemo) {
                  <span class="spinner"></span> Restaurando...
                } @else {
                  🔄 Restaurar Casos Demo
                }
              </button>
            }
          </div>
        } @else {
          @for (caso of casos; track caso.id) {
            <button
              type="button"
              class="case-item"
              [class.selected]="selectedId === caso.id"
              (click)="selectCaso.emit(caso.id)"
            >
              <div class="case-top">
                <span class="badge" [class]="'badge badge-estado-' + caso.estado">
                  {{ formatEstado(caso.estado) }}
                </span>
                <span class="badge" [class]="'badge badge-prioridad-' + caso.prioridad">
                  {{ caso.prioridad }}
                </span>
              </div>

              <div class="case-title">{{ caso.titulo }}</div>

              <div class="case-meta">
                <span>{{ caso.clienteNombre }}</span>
                <span>{{ caso.fechaCreacion | date: 'dd/MM HH:mm' }}</span>
              </div>

              <div class="case-footer">
                <span>{{ caso.totalRespuestas }} resp.</span>
                <div class="footer-pills">
                  <span
                    class="badge sla-pill"
                    [class]="'badge sla-pill sla-' + caso.estadoSla"
                    [title]="'Límite SLA: ' + (caso.fechaLimiteSla | date: 'dd/MM/yyyy HH:mm')"
                  >
                    {{ formatSla(caso.estadoSla) }}
                  </span>
                  @if (caso.tieneSugerenciaPendiente) {
                    <span class="badge badge-ia">✨ IA pendiente</span>
                  }
                </div>
              </div>
            </button>
          }
        }
      </div>

      @if (!papeleraMode) {
        <div class="pagination-bar">
          <button
            type="button"
            class="btn btn-sm"
            [disabled]="page <= 1 || loading"
            (click)="pageChange.emit(page - 1)"
          >
            ← Anterior
          </button>
          <span class="muted">Página {{ page }} de {{ totalPages || 1 }}</span>
          <button
            type="button"
            class="btn btn-sm"
            [disabled]="page >= totalPages || loading"
            (click)="pageChange.emit(page + 1)"
          >
            Siguiente →
          </button>
        </div>
      } @else if (isAnalista && papeleraCasos.length > 0) {
        <div class="pagination-bar">
          <button
            type="button"
            class="btn btn-soft btn-sm demo-btn-full"
            [disabled]="restoringDemo"
            (click)="restaurarDemo.emit()"
          >
            @if (restoringDemo) {
              <span class="spinner"></span> Restaurando...
            } @else {
              🔄 Restaurar Casos Demo
            }
          </button>
        </div>
      }
    </aside>
  `,
  styles: [
    `
      .list-panel {
        display: flex;
        flex-direction: column;
        height: calc(100vh - 108px);
        min-height: 520px;
        overflow: hidden;
      }
      .list-header {
        padding: 0.9rem 1rem 0.7rem;
        border-bottom: 1px solid var(--border-color);
        h2 {
          margin: 0;
          font-size: 1.05rem;
        }
      }
      .list-title-row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        flex-wrap: wrap;
        gap: 0.5rem;
      }
      .mode-tabs {
        display: flex;
        gap: 0.35rem;
        align-items: center;
      }
      .trash-item {
        cursor: default;
      }
      .trash-item:hover {
        background: var(--bg-surface);
      }
      .trash-actions {
        display: flex;
        justify-content: flex-end;
        margin-top: 0.15rem;
      }
      .demo-btn {
        margin-top: 0.75rem;
      }
      .demo-btn-full {
        width: 100%;
      }
      .muted {
        color: var(--text-secondary);
        font-size: 0.8rem;
      }
      .search-toolbar {
        display: grid;
        gap: 0.45rem;
        padding: 0.65rem 0.85rem;
        border-bottom: 1px solid var(--border-color);
        background: var(--bg-surface);
      }
      .search-input,
      .priority-select {
        font-size: 0.82rem;
        padding: 0.4rem 0.6rem;
      }
      .filter-bar {
        display: flex;
        flex-wrap: wrap;
        gap: 0.35rem;
        padding: 0.7rem 0.85rem;
        border-bottom: 1px solid var(--border-color);
        background: var(--bg-surface-alt);
      }
      .filter-pill {
        border: 1px solid var(--border-color);
        background: var(--bg-surface);
        border-radius: 999px;
        padding: 0.25rem 0.6rem;
        font-size: 0.76rem;
        font-weight: 600;
        cursor: pointer;
      }
      .filter-pill.active {
        background: var(--accent-primary);
        border-color: var(--accent-primary);
        color: #ffffff;
      }
      .cases-scroll {
        flex: 1;
        overflow-y: auto;
        padding: 0.75rem;
        display: grid;
        gap: 0.6rem;
        align-content: start;
      }
      .case-item {
        width: 100%;
        text-align: left;
        padding: 0.8rem;
        border-radius: calc(var(--radius-card) * 0.85);
        border: 1px solid var(--border-color);
        background: var(--bg-surface);
        cursor: pointer;
        display: grid;
        gap: 0.45rem;
        transition:
          border-color 180ms ease,
          background-color 180ms ease,
          transform 180ms cubic-bezier(0.16, 1, 0.3, 1);
      }
      .case-item:hover {
        background: var(--bg-surface-alt);
        transform: translateY(-1px);
      }
      .case-item.selected {
        border-color: var(--accent-primary);
        background: var(--accent-soft);
      }
      .case-top,
      .case-meta,
      .case-footer {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 0.5rem;
      }
      .case-title {
        font-weight: 600;
        font-size: 0.9rem;
        line-height: 1.35;
      }
      .case-meta,
      .case-footer {
        font-size: 0.76rem;
        color: var(--text-secondary);
      }
      .footer-pills {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        flex-wrap: wrap;
        justify-content: flex-end;
      }
      .sla-pill {
        font-size: 0.7rem;
        padding: 0.14rem 0.48rem;
      }
      .sla-EnTiempo {
        background: var(--success-bg);
        color: var(--success-text);
      }
      .sla-PorVencer {
        background: var(--warn-bg);
        color: var(--warn-text);
      }
      .sla-Vencido {
        background: var(--danger-bg);
        color: var(--danger-text);
      }
      .sla-Cumplido {
        background: var(--accent-soft);
        color: var(--accent-text);
      }
      .empty-box {
        padding: 2rem 1rem;
        text-align: center;
        color: var(--text-secondary);
        font-size: 0.88rem;
      }
      .pagination-bar {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 0.7rem 0.9rem;
        border-top: 1px solid var(--border-color);
        background: var(--bg-surface-alt);
      }
    `
  ]
})
export class CasosListComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly searchSubject = new Subject<string>();

  @Input({ required: true }) casos: CasoResumen[] = [];
  @Input() selectedId: string | null = null;
  @Input() estadoFilter: EstadoCaso | '' = '';
  @Input() prioridadFilter: PrioridadCaso | '' = '';
  @Input() busqueda = '';
  @Input() page = 1;
  @Input() totalPages = 1;
  @Input() totalCount = 0;
  @Input() loading = false;
  @Input() isAnalista = false;
  @Input() papeleraMode = false;
  @Input() papeleraCasos: CasoEliminado[] = [];
  @Input() papeleraCount = 0;
  @Input() restoringId: string | null = null;
  @Input() restoringDemo = false;

  @Output() selectCaso = new EventEmitter<string>();
  @Output() estadoFilterChange = new EventEmitter<EstadoCaso | ''>();
  @Output() prioridadFilterChange = new EventEmitter<PrioridadCaso | ''>();
  @Output() busquedaChange = new EventEmitter<string>();
  @Output() pageChange = new EventEmitter<number>();
  @Output() togglePapelera = new EventEmitter<boolean>();
  @Output() restaurarCaso = new EventEmitter<string>();
  @Output() restaurarDemo = new EventEmitter<void>();

  readonly filterOptions: readonly { label: string; value: EstadoCaso | '' }[] = [
    { label: 'Todos', value: '' },
    { label: 'Abierto', value: 'Abierto' },
    { label: 'En Proceso', value: 'EnProceso' },
    { label: 'Resuelto', value: 'Resuelto' },
    { label: 'Cerrado', value: 'Cerrado' }
  ];

  ngOnInit(): void {
    this.searchSubject
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((term) => this.busquedaChange.emit(term));
  }

  onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchSubject.next(value);
  }

  onPriorityChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as PrioridadCaso | '';
    this.prioridadFilterChange.emit(value);
  }

  formatEstado(estado: EstadoCaso): string {
    return estado === 'EnProceso' ? 'En Proceso' : estado;
  }

  formatSla(estadoSla: EstadoSla): string {
    switch (estadoSla) {
      case 'Cumplido':
        return '✅ SLA Cumplido';
      case 'PorVencer':
        return '🟡 SLA Por vencer';
      case 'Vencido':
        return '🔴 SLA Vencido';
      default:
        return '🟢 SLA En tiempo';
    }
  }
}
