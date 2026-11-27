import { signal } from '@angular/core';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from './auth.service';
import {
  IDLE_TIMEOUT_MS,
  IdleTimeoutService,
  LAST_ACTIVITY_STORAGE_KEY,
  WARNING_DELAY_MS
} from './idle-timeout.service';

describe('IdleTimeoutService', () => {
  let service: IdleTimeoutService;
  let router: Router;
  let isAuthenticatedSignal: ReturnType<typeof signal<boolean>>;
  let logoutSpy: jasmine.Spy<() => void>;

  beforeEach(() => {
    localStorage.clear();
    isAuthenticatedSignal = signal<boolean>(false);
    logoutSpy = jasmine.createSpy('logout').and.callFake(() => {
      isAuthenticatedSignal.set(false);
    });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            isAuthenticated: isAuthenticatedSignal,
            logout: logoutSpy
          }
        }
      ]
    });

    service = TestBed.inject(IdleTimeoutService);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    service.stopMonitoring();
    localStorage.clear();
  });

  it('debe activar showWarning() a los 9 minutos (540_000 ms) y decrementar secondsRemaining()', fakeAsync(() => {
    isAuthenticatedSignal.set(true);
    TestBed.flushEffects();

    tick(WARNING_DELAY_MS - 1);
    expect(service.showWarning()).toBeFalse();

    tick(1);
    expect(service.showWarning()).toBeTrue();
    expect(service.secondsRemaining()).toBe(60);

    tick(10_000);
    expect(service.secondsRemaining()).toBe(50);

    service.stopMonitoring();
  }));

  it('debe cancelar la advertencia y reiniciar el reloj al llamar resetTimer() o detectar interacción', fakeAsync(() => {
    isAuthenticatedSignal.set(true);
    TestBed.flushEffects();

    tick(WARNING_DELAY_MS);
    expect(service.showWarning()).toBeTrue();

    service.resetTimer();
    expect(service.showWarning()).toBeFalse();
    expect(service.secondsRemaining()).toBe(60);

    // Avanzar 5 minutos no debe disparar advertencia porque el reloj se reinició
    tick(300_000);
    expect(service.showWarning()).toBeFalse();

    // Llegar a los 9 minutos desde el reset vuelve a mostrarla, y un keydown la cancela
    tick(WARNING_DELAY_MS - 300_000);
    expect(service.showWarning()).toBeTrue();

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(service.showWarning()).toBeFalse();
    expect(logoutSpy).not.toHaveBeenCalled();

    service.stopMonitoring();
  }));

  it('debe cerrar sesión y navegar a /login?reason=idle tras 10 minutos (600_000 ms) sin actividad', fakeAsync(() => {
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    isAuthenticatedSignal.set(true);
    TestBed.flushEffects();

    tick(WARNING_DELAY_MS);
    expect(service.showWarning()).toBeTrue();

    tick(IDLE_TIMEOUT_MS - WARNING_DELAY_MS);

    expect(service.showWarning()).toBeFalse();
    expect(logoutSpy).toHaveBeenCalledTimes(1);
    expect(navigateSpy).toHaveBeenCalledWith(['/login'], {
      queryParams: { reason: 'idle' }
    });
  }));

  it('debe sincronizar actividad entre pestañas mediante StorageEvent en mesaayuda_last_activity', fakeAsync(() => {
    isAuthenticatedSignal.set(true);
    TestBed.flushEffects();

    tick(WARNING_DELAY_MS);
    expect(service.showWarning()).toBeTrue();

    const nowTs = Date.now();
    window.dispatchEvent(
      new StorageEvent('storage', {
        key: LAST_ACTIVITY_STORAGE_KEY,
        newValue: String(nowTs)
      })
    );

    expect(service.showWarning()).toBeFalse();

    // Sin nueva actividad, debe disparar nuevamente a los 9 minutos desde la actividad sincronizada
    tick(WARNING_DELAY_MS - 1_000);
    expect(service.showWarning()).toBeFalse();

    tick(1_000);
    expect(service.showWarning()).toBeTrue();

    service.stopMonitoring();
  }));
});
