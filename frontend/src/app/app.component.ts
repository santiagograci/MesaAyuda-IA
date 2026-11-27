import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { IdleTimeoutService } from './core/services/idle-timeout.service';
import { ThemeService } from './core/services/theme.service';

/**
 * Componente raíz que inicializa el motor de temas, monitorea inactividad
 * y renderiza las rutas activas.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    @if (idleService.showWarning()) {
      <div class="idle-warning-banner" role="alert" aria-live="assertive">
        <span>
          ⏳ Tu sesión expirará en <strong>{{ idleService.secondsRemaining() }}s</strong> por inactividad.
        </span>
        <button type="button" class="btn btn-primary btn-sm" (click)="idleService.resetTimer()">
          Seguir conectado
        </button>
      </div>
    }
    <router-outlet />
  `,
  styles: [
    `
      .idle-warning-banner {
        position: fixed;
        top: 0.9rem;
        left: 50%;
        transform: translateX(-50%);
        z-index: 9999;
        display: flex;
        align-items: center;
        justify-content: space-between;
        flex-wrap: wrap;
        gap: 0.85rem;
        max-width: calc(100vw - 2rem);
        padding: 0.7rem 1rem;
        border-radius: var(--radius-card);
        background: var(--warn-bg);
        color: var(--warn-text);
        border: 1px solid var(--border-color);
        box-shadow: var(--shadow-card);
        font-size: 0.9rem;
      }
    `
  ]
})
export class AppComponent {
  // Se inyecta al arranque para aplicar el tema persistido en localStorage desde el inicio.
  private readonly _themeService = inject(ThemeService);
  readonly idleService = inject(IdleTimeoutService);
}
