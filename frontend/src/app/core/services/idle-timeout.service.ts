import { Injectable, NgZone, OnDestroy, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Tiempo total de inactividad permitido antes de cerrar la sesión (10 minutos). */
export const IDLE_TIMEOUT_MS = 600_000;

/** Ventana previa de advertencia antes del cierre automático (1 minuto). */
export const WARNING_BEFORE_MS = 60_000;

/** Tiempo transcurrido sin actividad tras el cual se muestra la advertencia (9 minutos). */
export const WARNING_DELAY_MS = IDLE_TIMEOUT_MS - WARNING_BEFORE_MS;

/** Intervalo mínimo de throttling para procesar eventos de actividad DOM. */
export const ACTIVITY_THROTTLE_MS = 1_000;

/** Clave de localStorage usada para sincronizar actividad entre pestañas. */
export const LAST_ACTIVITY_STORAGE_KEY = 'mesaayuda_last_activity';

const ACTIVITY_EVENTS = [
  'mousemove',
  'mousedown',
  'keydown',
  'scroll',
  'touchstart',
  'click'
] as const;

/**
 * Servicio singleton que monitorea la inactividad del usuario autenticado,
 * sincroniza actividad multi-pestaña por localStorage, muestra advertencia
 * en el último minuto y cierra la sesión automáticamente tras 10 minutos.
 */
@Injectable({ providedIn: 'root' })
export class IdleTimeoutService implements OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly ngZone = inject(NgZone);

  /** Indica si debe mostrarse la advertencia preventiva de cierre de sesión. */
  readonly showWarning = signal<boolean>(false);

  /** Segundos restantes de la ventana de advertencia antes de cerrar sesión. */
  readonly secondsRemaining = signal<number>(60);

  private monitoring = false;
  private lastActivityAt = 0;
  private warningTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private countdownIntervalId: ReturnType<typeof setInterval> | null = null;
  private logoutTimeoutId: ReturnType<typeof setTimeout> | null = null;

  private readonly handleActivityBound = (): void => this.onUserActivity();
  private readonly handleStorageBound = (event: StorageEvent): void => this.onStorageChange(event);

  constructor() {
    effect(
      () => {
        if (this.authService.isAuthenticated()) {
          this.startMonitoring();
        } else {
          this.stopMonitoring();
        }
      },
      { allowSignalWrites: true }
    );
  }

  /** Inicia el monitoreo de actividad y la sincronización multi-pestaña si aún no está activo. */
  startMonitoring(): void {
    if (this.monitoring) {
      return;
    }

    this.monitoring = true;
    this.lastActivityAt = Date.now();
    this.persistLastActivity(this.lastActivityAt);
    this.showWarning.set(false);
    this.secondsRemaining.set(Math.floor(WARNING_BEFORE_MS / 1000));

    if (typeof window !== 'undefined') {
      this.ngZone.runOutsideAngular(() => {
        for (const eventName of ACTIVITY_EVENTS) {
          window.addEventListener(eventName, this.handleActivityBound, { passive: true });
        }
        window.addEventListener('storage', this.handleStorageBound);
      });
    }

    this.scheduleTimers(0);
  }

  /** Detiene temporizadores, listeners DOM y limpia el estado de advertencia. */
  stopMonitoring(): void {
    this.monitoring = false;
    this.clearTimers();

    if (typeof window !== 'undefined') {
      for (const eventName of ACTIVITY_EVENTS) {
        window.removeEventListener(eventName, this.handleActivityBound);
      }
      window.removeEventListener('storage', this.handleStorageBound);
    }

    this.showWarning.set(false);
    this.secondsRemaining.set(Math.floor(WARNING_BEFORE_MS / 1000));
  }

  /** Reinicia manualmente el contador de inactividad y oculta la advertencia. */
  resetTimer(): void {
    if (!this.authService.isAuthenticated()) {
      return;
    }

    this.lastActivityAt = Date.now();
    this.persistLastActivity(this.lastActivityAt);
    this.showWarning.set(false);
    this.secondsRemaining.set(Math.floor(WARNING_BEFORE_MS / 1000));
    this.scheduleTimers(0);
  }

  ngOnDestroy(): void {
    this.stopMonitoring();
  }

  private onUserActivity(): void {
    if (!this.monitoring) {
      return;
    }

    const now = Date.now();
    const warningWasVisible = this.showWarning();
    if (!warningWasVisible && now - this.lastActivityAt < ACTIVITY_THROTTLE_MS) {
      return;
    }

    this.lastActivityAt = now;
    this.persistLastActivity(now);
    this.scheduleTimers(0);

    if (warningWasVisible) {
      this.ngZone.run(() => {
        this.showWarning.set(false);
        this.secondsRemaining.set(Math.floor(WARNING_BEFORE_MS / 1000));
      });
    }
  }

  private onStorageChange(event: StorageEvent): void {
    if (!this.monitoring || event.key !== LAST_ACTIVITY_STORAGE_KEY || !event.newValue) {
      return;
    }

    const otherTimestamp = Number(event.newValue);
    if (Number.isNaN(otherTimestamp) || otherTimestamp <= this.lastActivityAt) {
      return;
    }

    this.lastActivityAt = otherTimestamp;
    const elapsed = Math.max(0, Date.now() - otherTimestamp);
    const warningWasVisible = this.showWarning();

    this.scheduleTimers(elapsed);

    if (warningWasVisible && elapsed < WARNING_DELAY_MS) {
      this.ngZone.run(() => {
        this.showWarning.set(false);
        this.secondsRemaining.set(Math.floor(WARNING_BEFORE_MS / 1000));
      });
    }
  }

  private scheduleTimers(elapsedMs: number): void {
    this.clearTimers();

    const toWarning = Math.max(0, WARNING_DELAY_MS - elapsedMs);
    const toLogout = Math.max(0, IDLE_TIMEOUT_MS - elapsedMs);

    if (toLogout <= 0) {
      this.ngZone.run(() => this.handleIdleLogout());
      return;
    }

    this.ngZone.runOutsideAngular(() => {
      if (toWarning > 0) {
        this.warningTimeoutId = setTimeout(() => {
          this.ngZone.run(() => this.enterWarningPhase());
        }, toWarning);
      } else {
        this.ngZone.run(() => this.enterWarningPhase());
      }

      this.logoutTimeoutId = setTimeout(() => {
        this.ngZone.run(() => this.handleIdleLogout());
      }, toLogout);
    });
  }

  private enterWarningPhase(): void {
    if (!this.monitoring) {
      return;
    }

    this.updateRemainingSeconds();
    this.showWarning.set(true);

    if (this.countdownIntervalId !== null) {
      clearInterval(this.countdownIntervalId);
    }

    this.ngZone.runOutsideAngular(() => {
      this.countdownIntervalId = setInterval(() => {
        this.ngZone.run(() => this.updateRemainingSeconds());
      }, 1_000);
    });
  }

  private updateRemainingSeconds(): void {
    const remainingMs = Math.max(0, this.lastActivityAt + IDLE_TIMEOUT_MS - Date.now());
    this.secondsRemaining.set(Math.max(0, Math.ceil(remainingMs / 1000)));
  }

  private handleIdleLogout(): void {
    this.stopMonitoring();
    this.clearStoredActivity();
    this.authService.logout();
    void this.router.navigate(['/login'], { queryParams: { reason: 'idle' } });
  }

  private clearTimers(): void {
    if (this.warningTimeoutId !== null) {
      clearTimeout(this.warningTimeoutId);
      this.warningTimeoutId = null;
    }
    if (this.countdownIntervalId !== null) {
      clearInterval(this.countdownIntervalId);
      this.countdownIntervalId = null;
    }
    if (this.logoutTimeoutId !== null) {
      clearTimeout(this.logoutTimeoutId);
      this.logoutTimeoutId = null;
    }
  }

  private persistLastActivity(timestamp: number): void {
    if (typeof localStorage === 'undefined') {
      return;
    }
    try {
      localStorage.setItem(LAST_ACTIVITY_STORAGE_KEY, String(timestamp));
    } catch {
      // Ignora fallos de cuota o restricciones de almacenamiento.
    }
  }

  private clearStoredActivity(): void {
    if (typeof localStorage === 'undefined') {
      return;
    }
    try {
      localStorage.removeItem(LAST_ACTIVITY_STORAGE_KEY);
    } catch {
      // Ignora restricciones de almacenamiento.
    }
  }
}
