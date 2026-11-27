import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DemoUser } from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { OfimaLogoComponent } from '../../shared/ofima-logo/ofima-logo.component';
import { ThemeCustomizerComponent } from '../../shared/theme-customizer/theme-customizer.component';

const FALLBACK_DEMO_USERS: DemoUser[] = [
  {
    nombre: 'Carlos Mendoza - Contabilidad',
    email: 'cliente@empresa.com',
    rol: 'Cliente'
  },
  {
    nombre: 'Laura Gómez - Tesorería',
    email: 'cliente2@empresa.com',
    rol: 'Cliente'
  },
  {
    nombre: 'Santiago Graciano - Analista Soporte',
    email: 'analista@ofima.com',
    rol: 'Analista'
  }
];

/**
 * Vista de inicio de sesión con formulario reactivo y accesos rápidos de demostración.
 */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, ThemeCustomizerComponent, OfimaLogoComponent],
  template: `
    <div class="login-page">
      <div class="login-topbar">
        <app-theme-customizer />
      </div>

      <main class="login-card card-surface">
        <header class="login-header">
          <app-ofima-logo variant="full" />
          <p>Ingresa con tus credenciales o usa un acceso rápido de demostración.</p>
        </header>

        @if (idleNotice()) {
          <div class="idle-notice" role="status">
            🔒 Tu sesión se cerró automáticamente tras 10 minutos de inactividad por seguridad.
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" class="login-form">
          <div class="field">
            <label for="email">Correo electrónico</label>
            <input
              id="email"
              type="email"
              class="form-control"
              formControlName="email"
              placeholder="usuario@empresa.com"
            />
            @if (form.controls.email.touched && form.controls.email.invalid) {
              <small class="field-error">Ingresa un correo electrónico válido.</small>
            }
          </div>

          <div class="field">
            <label for="password">Contraseña</label>
            <input
              id="password"
              type="password"
              class="form-control"
              formControlName="password"
              placeholder="••••••••"
            />
            @if (form.controls.password.touched && form.controls.password.invalid) {
              <small class="field-error">La contraseña es obligatoria.</small>
            }
          </div>

          @if (errorMsg()) {
            <div class="alert-error" role="alert">{{ errorMsg() }}</div>
          }

          <button type="submit" class="btn btn-primary" [disabled]="loading()">
            @if (loading()) {
              <span class="spinner"></span> Iniciando sesión...
            } @else {
              Ingresar al Portal
            }
          </button>
        </form>

        <section class="demo-section">
          <div class="demo-heading">Acceso Rápido Demo (1 clic · clave Ofima2026*)</div>
          <div class="demo-grid">
            @for (user of demoUsers(); track user.email) {
              <button
                type="button"
                class="btn btn-sm demo-btn"
                [disabled]="loading()"
                (click)="quickLogin(user)"
              >
                <span class="demo-name">{{ user.nombre }}</span>
                <span class="badge" [class.badge-ia]="user.rol === 'Analista'">{{ user.rol }}</span>
              </button>
            }
          </div>
        </section>
      </main>
    </div>
  `,
  styles: [
    `
      .login-page {
        min-height: 100vh;
        display: grid;
        place-items: center;
        padding: 1.5rem;
        position: relative;
      }
      .login-topbar {
        position: absolute;
        top: 1rem;
        right: 1.25rem;
      }
      .login-card {
        width: 100%;
        max-width: 440px;
        padding: 2rem;
      }
      .login-header {
        margin-bottom: 1.5rem;
        display: grid;
        gap: 0.75rem;
        p {
          margin: 0;
          color: var(--text-secondary);
          font-size: 0.9rem;
        }
      }
      .login-form {
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
      .field-error {
        color: var(--danger-text);
        font-size: 0.78rem;
      }
      .demo-section {
        margin-top: 1.5rem;
        padding-top: 1.15rem;
        border-top: 1px solid var(--border-color);
      }
      .demo-heading {
        font-size: 0.78rem;
        font-weight: 600;
        color: var(--text-secondary);
        margin-bottom: 0.65rem;
      }
      .demo-grid {
        display: grid;
        gap: 0.5rem;
      }
      .demo-btn {
        justify-content: space-between;
        width: 100%;
      }
      .demo-name {
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .idle-notice {
        margin-bottom: 1rem;
        padding: 0.75rem 0.9rem;
        border-radius: calc(var(--radius-card) * 0.75);
        background: var(--warn-bg);
        color: var(--warn-text);
        border: 1px solid var(--border-color);
        font-size: 0.85rem;
        font-weight: 500;
      }
    `
  ]
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal<boolean>(false);
  readonly errorMsg = signal<string>('');
  readonly idleNotice = signal<boolean>(false);
  readonly demoUsers = signal<DemoUser[]>(FALLBACK_DEMO_USERS);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  ngOnInit(): void {
    if (this.authService.isAuthenticated()) {
      void this.router.navigate(['/casos']);
      return;
    }

    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => {
        this.idleNotice.set(params.get('reason') === 'idle');
      });

    this.authService.getDemoUsers().subscribe({
      next: (users) => {
        if (users.length > 0) {
          this.demoUsers.set(users);
        }
      },
      error: () => {
        this.demoUsers.set(FALLBACK_DEMO_USERS);
      }
    });
  }

  quickLogin(user: DemoUser): void {
    this.form.setValue({
      email: user.email,
      password: 'Ofima2026*'
    });
    this.submit();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMsg.set('');
    this.idleNotice.set(false);

    this.authService.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.loading.set(false);
        void this.router.navigate(['/casos']);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        if (err instanceof HttpErrorResponse) {
          const body = err.error as { mensaje?: string; detail?: string } | null;
          this.errorMsg.set(
            body?.mensaje || body?.detail || 'No fue posible iniciar sesión. Verifica tus credenciales.'
          );
        } else {
          this.errorMsg.set('Error de conexión con el servidor.');
        }
      }
    });
  }
}
