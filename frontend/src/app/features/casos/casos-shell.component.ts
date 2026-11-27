import { Component, OnInit, inject, signal } from '@angular/core';
import { CasoDetalle, CasoEliminado, CasoResumen, EstadoCaso } from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { CasosService } from '../../core/services/casos.service';
import { OfimaLogoComponent } from '../../shared/ofima-logo/ofima-logo.component';
import { ThemeCustomizerComponent } from '../../shared/theme-customizer/theme-customizer.component';
import { CasoCreateComponent } from './caso-create/caso-create.component';
import { CasoDetailComponent } from './caso-detail/caso-detail.component';
import { CasosListComponent } from './casos-list/casos-list.component';

/**
 * Contenedor principal Split-Screen del portal MesaAyuda IA.
 */
@Component({
  selector: 'app-casos-shell',
  standalone: true,
  imports: [
    ThemeCustomizerComponent,
    OfimaLogoComponent,
    CasosListComponent,
    CasoDetailComponent,
    CasoCreateComponent
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

          <button type="button" class="btn btn-primary btn-sm" (click)="openCreate()">
            + Nuevo Caso
          </button>

          <button type="button" class="btn btn-sm" (click)="authService.logout()">
            Cerrar Sesión
          </button>
        </div>
      </header>

      <main class="split-workspace">
        <app-casos-list
          [casos]="casos()"
          [selectedId]="selectedCaso()?.id ?? null"
          [estadoFilter]="estadoFilter()"
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

  ngOnInit(): void {
    this.loadCasos(true);
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
  }

  onCasoActualizado(detalle: CasoDetalle): void {
    this.selectedCaso.set(detalle);
    this.loadCasos(false);
  }

  onCasoEliminado(): void {
    this.selectedCaso.set(null);
    this.infoMsg.set('Caso enviado a la papelera de reciclaje.');
    this.loadCasos(true);
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
      },
      error: () => {
        this.restoringDemo.set(false);
        this.errorMsg.set('No se pudieron restaurar los casos de demostración.');
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

    this.casosService.listarCasos(this.estadoFilter(), this.page(), 8).subscribe({
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
