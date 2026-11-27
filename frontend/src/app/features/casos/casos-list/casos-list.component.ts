import { DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CasoResumen, EstadoCaso } from '../../../core/models/api.models';

/**
 * Columna izquierda del Split-Screen: filtros por estado, listado paginado y controles de página.
 */
@Component({
  selector: 'app-casos-list',
  standalone: true,
  imports: [DatePipe],
  template: `
    <aside class="list-panel card-surface">
      <div class="list-header">
        <div>
          <h2>Casos de Soporte</h2>
          <span class="muted">{{ totalCount }} registros</span>
        </div>
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

      <div class="cases-scroll">
        @if (loading) {
          <div class="empty-box"><span class="spinner"></span> Cargando casos...</div>
        } @else if (casos.length === 0) {
          <div class="empty-box">No hay casos registrados con el filtro actual.</div>
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
                @if (caso.tieneSugerenciaPendiente) {
                  <span class="badge badge-ia">✨ IA pendiente</span>
                }
              </div>
            </button>
          }
        }
      </div>

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
        padding: 1rem 1rem 0.65rem;
        border-bottom: 1px solid var(--border-color);
        h2 {
          margin: 0;
          font-size: 1.05rem;
        }
      }
      .muted {
        color: var(--text-secondary);
        font-size: 0.8rem;
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
        transition: border-color 0.15s ease, background-color 0.15s ease;
      }
      .case-item:hover {
        background: var(--bg-surface-alt);
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
export class CasosListComponent {
  @Input({ required: true }) casos: CasoResumen[] = [];
  @Input() selectedId: string | null = null;
  @Input() estadoFilter: EstadoCaso | '' = '';
  @Input() page = 1;
  @Input() totalPages = 1;
  @Input() totalCount = 0;
  @Input() loading = false;

  @Output() selectCaso = new EventEmitter<string>();
  @Output() estadoFilterChange = new EventEmitter<EstadoCaso | ''>();
  @Output() pageChange = new EventEmitter<number>();

  readonly filterOptions: readonly { label: string; value: EstadoCaso | '' }[] = [
    { label: 'Todos', value: '' },
    { label: 'Abierto', value: 'Abierto' },
    { label: 'En Proceso', value: 'EnProceso' },
    { label: 'Resuelto', value: 'Resuelto' },
    { label: 'Cerrado', value: 'Cerrado' }
  ];

  formatEstado(estado: EstadoCaso): string {
    return estado === 'EnProceso' ? 'En Proceso' : estado;
  }
}
