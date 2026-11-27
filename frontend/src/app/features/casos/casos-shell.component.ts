import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import {
  CasoDetalle,
  CasoEliminado,
  CasoResumen,
  EstadoCaso,
  MetricasMesaAyuda,
  PrioridadCaso
} from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { CasosService } from '../../core/services/casos.service';
import { OfimaLogoComponent } from '../../shared/ofima-logo/ofima-logo.component';
import { ThemeCustomizerComponent } from '../../shared/theme-customizer/theme-customizer.component';
import { CasoCreateComponent } from './caso-create/caso-create.component';
import { CasoDetailComponent } from './caso-detail/caso-detail.component';
import { CasosListComponent } from './casos-list/casos-list.component';
import { KnowledgeBaseModalComponent } from './knowledge-base-modal/knowledge-base-modal.component';

/**
 * Contenedor principal Split-Screen del portal MesaAyuda IA.
 */
@Component({
  selector: 'app-casos-shell',
  standalone: true,
  imports: [
    DecimalPipe,
    ThemeCustomizerComponent,
    OfimaLogoComponent,
    CasosListComponent,
    CasoDetailComponent,
    CasoCreateComponent,
    KnowledgeBaseModalComponent
  ],
  template: `
    <div class="shell-wrapper">
      <header class="topbar card-surface">
        <app-ofima-logo variant="compact" />

        <div class="user-area">
          @if (authService.currentUser(); as user) {
            <div class="user-pill">
              <span>{{ user.nombre }}</span>
              <span class="badge" [class.badge-ia]="user.rol === 'Analista'">{{ user.rol }}</span>
            </div>
          }

          <app-theme-customizer />

          @if (authService.isAnalista()) {
            <button
              type="button"
              class="btn btn-soft btn-sm"
              data-testid="open-kb-modal-btn"
              (click)="kbModalOpen.set(true)"
            >
              📚 Base Conocimiento RAG
            </button>
          }

          <button type="button" class="btn btn-primary btn-sm" (click)="openCreate()">
            + Nuevo Caso
          </button>

          <button type="button" class="btn btn-sm" (click)="authService.logout()">
            Cerrar Sesión
          </button>
        </div>
      </header>

      @if (metricas(); as m) {
        <section class="kpi-strip" aria-label="Indicadores ejecutivos MesaAyuda IA">
          <article class="kpi-card card-surface">
            <span class="kpi-label">Casos Activos</span>
            <strong class="kpi-value">{{ m.casosAbiertos + m.casosEnProceso }}</strong>
            <span class="kpi-sub">
              {{ m.casosAbiertos }} abiertos · {{ m.casosEnProceso }} en proceso · {{ m.casosResueltos }} resueltos
            </span>
          </article>

          <article class="kpi-card card-surface" [class.kpi-danger]="m.casosCriticosActivos > 0">
            <span class="kpi-label">Severidad Crítica</span>
            <strong class="kpi-value">{{ m.casosCriticosActivos }}</strong>
            <span class="kpi-sub">
              {{ m.casosCriticosActivos > 0 ? 'Requiere atención prioritaria' : 'Sin incidentes críticos activos' }}
            </span>
          </article>

          <article class="kpi-card card-surface">
            <span class="kpi-label">Adopción Asistente IA</span>
            <strong class="kpi-value">{{ m.tasaAdopcionIaPorcentaje | number: '1.0-1' }}%</strong>
            <span class="kpi-sub">
              {{ m.sugerenciasIaAprobadas }} de {{ m.sugerenciasIaGeneradas }} sugerencias aprobadas
            </span>
          </article>

          <article class="kpi-card card-surface">
            <span class="kpi-label">Consumo FinOps RAG</span>
            <strong class="kpi-value">{{ m.tokensTotalesConsumidos | number }}</strong>
            <span class="kpi-sub">tokens procesados por IA</span>
          </article>

          <article class="kpi-card card-surface">
            <span class="kpi-label">Satisfacción CSAT</span>
            <strong class="kpi-value">
              {{ m.promedioCsat !== null ? '⭐ ' + (m.promedioCsat | number: '1.1-1') + ' / 5.0' : 'Sin datos' }}
            </strong>
            <span class="kpi-sub">
              {{ m.totalEncuestasCsat }} encuesta(s) · {{ deflexionesCount() }} deflexión(es)
            </span>
          </article>
        </section>
      }

      <main class="split-workspace">
        <app-casos-list
          [casos]="casos()"
          [selectedId]="selectedCaso()?.id ?? null"
          [estadoFilter]="estadoFilter()"
          [prioridadFilter]="prioridadFilter()"
          [busqueda]="busqueda()"
          [page]="page()"
          [totalPages]="totalPages()"
          [totalCount]="totalCount()"
          [loading]="listLoading()"
          [isAnalista]="authService.isAnalista()"
          [papeleraMode]="papeleraMode()"
          [papeleraCasos]="papeleraCasos()"
          [papeleraCount]="papeleraCasos().length"
          [restoringId]="restoringId()"
          [restoringDemo]="restoringDemo()"
          (selectCaso)="selectCaso($event)"
          (estadoFilterChange)="onEstadoFilterChange($event)"
          (prioridadFilterChange)="onPrioridadFilterChange($event)"
          (busquedaChange)="onBusquedaChange($event)"
          (pageChange)="onPageChange($event)"
          (togglePapelera)="onTogglePapelera($event)"
          (restaurarCaso)="onRestaurarCaso($event)"
          (restaurarDemo)="onRestaurarDemo()"
        />

        <div class="detail-pane">
          @if (errorMsg()) {
            <div class="alert-error" role="alert">{{ errorMsg() }}</div>
          }
          @if (infoMsg()) {
            <div class="alert-info" role="status">{{ infoMsg() }}</div>
          }

          @if (creating()) {
            <app-caso-create
              (created)="onCasoCreated($event)"
              (cancelled)="creating.set(false)"
              (deflected)="onCasoDeflected()"
            />
          } @else if (detailLoading()) {
            <div class="placeholder card-surface">
              <span class="spinner"></span> Cargando detalle del caso...
            </div>
          } @else {
            @if (selectedCaso(); as detalle) {
              <app-caso-detail
                [caso]="detalle"
                (casoActualizado)="onCasoActualizado($event)"
                (casoEliminado)="onCasoEliminado()"
              />
            } @else {
              <div class="placeholder card-surface">
                <h3>Selecciona un caso del panel izquierdo</h3>
                <p>O haz clic en <strong>+ Nuevo Caso</strong> para registrar una consulta sobre Ofima ERP.</p>
              </div>
            }
          }
        </div>
      </main>

      @if (kbModalOpen()) {
        <app-knowledge-base-modal (close)="kbModalOpen.set(false)" />
      }
    </div>
  `,
  styles: [
    `
      .topbar {
        display: flex;
        justify-content: space-between;
        align-items: center;
        flex-wrap: wrap;
        gap: 0.75rem;
        padding: 0.7rem 1.25rem 0.7rem 1.5rem;
        overflow: visible;
        border-radius: 0;
        border-left: 0;
        border-right: 0;
        border-top: 0;
      }
      .user-area {
        display: flex;
        align-items: center;
        flex-wrap: wrap;
        gap: 0.55rem;
      }
      .user-pill {
        display: inline-flex;
        align-items: center;
        gap: 0.45rem;
        padding: 0.3rem 0.65rem;
        border-radius: 999px;
        background: var(--bg-surface-alt);
        border: 1px solid var(--border-color);
        font-size: 0.8rem;
        font-weight: 600;
      }
      .detail-pane {
        display: grid;
        gap: 0.75rem;
        align-content: start;
      }
      .kpi-strip {
        display: grid;
        grid-template-columns: repeat(5, minmax(0, 1fr));
        gap: 0.75rem;
        padding: 0.85rem 1.25rem 0;
      }
      .kpi-card {
        padding: 0.75rem 0.95rem;
        display: grid;
        gap: 0.2rem;
      }
      .kpi-label {
        font-size: 0.74rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.03em;
        color: var(--text-secondary);
      }
      .kpi-value {
        font-size: 1.4rem;
        line-height: 1.15;
        color: var(--accent-text);
      }
      .kpi-sub {
        font-size: 0.76rem;
        color: var(--text-secondary);
      }
      .kpi-card.kpi-danger {
        border-color: var(--danger-border);
        background: var(--danger-bg);
        .kpi-value,
        .kpi-label {
          color: var(--danger-text);
        }
      }
      @media (max-width: 960px) {
        .kpi-strip {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }
      }
      @media (max-width: 540px) {
        .kpi-strip {
          grid-template-columns: 1fr;
        }
      }
      .placeholder {
        padding: 3rem 1.5rem;
        text-align: center;
        color: var(--text-secondary);
        h3 {
          margin: 0 0 0.4rem;
          color: var(--text-primary);
        }
        p {
          margin: 0;
        }
      }
    `
  ]
})
export class CasosShellComponent implements OnInit {
  private readonly casosService = inject(CasosService);
  readonly authService = inject(AuthService);

  readonly casos = signal<CasoResumen[]>([]);
  readonly selectedCaso = signal<CasoDetalle | null>(null);
  readonly estadoFilter = signal<EstadoCaso | ''>('');
  readonly prioridadFilter = signal<PrioridadCaso | ''>('');
  readonly busqueda = signal<string>('');
  readonly kbModalOpen = signal<boolean>(false);
  readonly page = signal<number>(1);
  readonly totalPages = signal<number>(1);
  readonly totalCount = signal<number>(0);

  readonly papeleraMode = signal<boolean>(false);
  readonly papeleraCasos = signal<CasoEliminado[]>([]);
  readonly restoringId = signal<string | null>(null);
  readonly restoringDemo = signal<boolean>(false);

  readonly listLoading = signal<boolean>(false);
  readonly detailLoading = signal<boolean>(false);
  readonly creating = signal<boolean>(false);
  readonly errorMsg = signal<string>('');
  readonly infoMsg = signal<string>('');
  readonly metricas = signal<MetricasMesaAyuda | null>(null);
  readonly deflexionesCount = signal<number>(this.loadDeflexionesCount());

  ngOnInit(): void {
    this.loadCasos(true);
    this.loadMetricas();
    if (this.authService.isAnalista()) {
      this.loadPapelera();
    }
  }

  openCreate(): void {
    this.creating.set(true);
  }

  onEstadoFilterChange(estado: EstadoCaso | ''): void {
    this.creating.set(false);
    this.estadoFilter.set(estado);
    this.page.set(1);
    this.loadCasos(true);
  }

  onPrioridadFilterChange(prioridad: PrioridadCaso | ''): void {
    this.creating.set(false);
    this.prioridadFilter.set(prioridad);
    this.page.set(1);
    this.loadCasos(true);
  }

  onBusquedaChange(term: string): void {
    this.creating.set(false);
    this.busqueda.set(term);
    this.page.set(1);
    this.loadCasos(true);
  }

  onPageChange(nextPage: number): void {
    this.creating.set(false);
    this.page.set(Math.max(1, nextPage));
    this.loadCasos(true);
  }

  selectCaso(id: string): void {
    this.creating.set(false);
    this.detailLoading.set(true);
    this.errorMsg.set('');

    this.casosService.obtenerCaso(id).subscribe({
      next: (detalle) => {
        this.detailLoading.set(false);
        this.selectedCaso.set(detalle);
      },
      error: () => {
        this.detailLoading.set(false);
        this.errorMsg.set('No se pudo cargar el detalle del caso seleccionado.');
      }
    });
  }

  onCasoCreated(detalle: CasoDetalle): void {
    this.creating.set(false);
    this.selectedCaso.set(detalle);
    this.page.set(1);
    this.loadCasos(false);
    this.loadMetricas();
  }

  onCasoDeflected(): void {
    this.creating.set(false);
    const next = this.deflexionesCount() + 1;
    this.deflexionesCount.set(next);
    if (typeof localStorage !== 'undefined') {
      try {
        localStorage.setItem('mesaayuda_deflexiones_count', String(next));
      } catch {
        // Ignora fallos de almacenamiento local.
      }
    }
    this.infoMsg.set(
      '✅ ¡Excelente! Tu duda fue resuelta con la guía oficial sin necesidad de abrir un ticket.'
    );
  }

  private loadDeflexionesCount(): number {
    if (typeof localStorage === 'undefined') {
      return 0;
    }
    const raw = Number(localStorage.getItem('mesaayuda_deflexiones_count') ?? '0');
    return Number.isFinite(raw) && raw > 0 ? raw : 0;
  }

  onCasoActualizado(detalle: CasoDetalle): void {
    this.selectedCaso.set(detalle);
    this.loadCasos(false);
    this.loadMetricas();
  }

  onCasoEliminado(): void {
    this.selectedCaso.set(null);
    this.infoMsg.set('Caso enviado a la papelera de reciclaje.');
    this.loadCasos(true);
    this.loadMetricas();
    if (this.authService.isAnalista()) {
      this.loadPapelera();
    }
  }

  onTogglePapelera(active: boolean): void {
    this.papeleraMode.set(active);
    this.creating.set(false);
    this.infoMsg.set('');
    if (active) {
      this.loadPapelera();
    }
  }

  onRestaurarCaso(id: string): void {
    this.restoringId.set(id);
    this.errorMsg.set('');
    this.infoMsg.set('');

    this.casosService.restaurarCaso(id).subscribe({
      next: (restaurado) => {
        this.restoringId.set(null);
        this.papeleraMode.set(false);
        this.selectedCaso.set(restaurado);
        this.infoMsg.set(`Caso "${restaurado.titulo}" restaurado desde la papelera.`);
        this.loadPapelera();
        this.loadCasos(false);
        this.loadMetricas();
      },
      error: () => {
        this.restoringId.set(null);
        this.errorMsg.set('No se pudo restaurar el caso desde la papelera.');
      }
    });
  }

  onRestaurarDemo(): void {
    this.restoringDemo.set(true);
    this.errorMsg.set('');
    this.infoMsg.set('');

    this.casosService.restaurarDatosDemo().subscribe({
      next: (res) => {
        this.restoringDemo.set(false);
        this.papeleraMode.set(false);
        this.infoMsg.set(res.mensaje);
        this.loadPapelera();
        this.loadCasos(true);
        this.loadMetricas();
      },
      error: () => {
        this.restoringDemo.set(false);
        this.errorMsg.set('No se pudieron restaurar los casos de demostración.');
      }
    });
  }

  private loadMetricas(): void {
    this.casosService.obtenerMetricas().subscribe({
      next: (data) => {
        this.metricas.set(data);
      },
      error: () => {
        // Mantiene la interfaz operativa si las métricas fallan de forma transitoria.
      }
    });
  }

  private loadPapelera(): void {
    this.casosService.listarPapelera().subscribe({
      next: (items) => {
        this.papeleraCasos.set(items);
      },
      error: () => {
        this.errorMsg.set('No se pudo consultar la papelera de reciclaje.');
      }
    });
  }

  private loadCasos(autoSelectFirst: boolean): void {
    this.listLoading.set(true);
    this.errorMsg.set('');

    this.casosService
      .listarCasos(
        this.estadoFilter(),
        this.page(),
        8,
        this.prioridadFilter(),
        this.busqueda()
      )
      .subscribe({
      next: (res) => {
        this.listLoading.set(false);

        if (res.items.length === 0 && this.page() > 1) {
          this.page.update((p) => p - 1);
          this.loadCasos(true);
          return;
        }

        this.casos.set(res.items);
        this.totalPages.set(res.totalPages);
        this.totalCount.set(res.totalCount);

        if (res.items.length === 0 && !this.creating()) {
          this.selectedCaso.set(null);
          return;
        }

        const currentSelected = this.selectedCaso();
        const stillInPage = currentSelected
          ? res.items.some((item) => item.id === currentSelected.id)
          : false;

        if ((autoSelectFirst || !stillInPage) && res.items.length > 0 && !this.creating()) {
          this.selectCaso(res.items[0].id);
        }
      },
      error: () => {
        this.listLoading.set(false);
        this.errorMsg.set('Error al obtener la lista de casos.');
      }
    });
  }
}
