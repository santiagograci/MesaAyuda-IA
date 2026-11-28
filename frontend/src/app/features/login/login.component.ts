import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DemoUser, RolUsuario } from '../../core/models/api.models';
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

interface HeroHighlight {
  icon: string;
  titulo: string;
  descripcion: string;
}

const HERO_HIGHLIGHTS: readonly HeroHighlight[] = [
  {
    icon: '🛡️',
    titulo: 'Deflexión Preventiva RAG',
    descripcion: 'Guías citadas antes de crear el ticket para reducir carga operativa'
  },
  {
    icon: '⚡',
    titulo: 'Copiloto IA con Streaming SSE',
    descripcion: 'Borradores técnicos auditables por el analista antes de publicar'
  },
  {
    icon: '📊',
    titulo: 'Gobernanza SLA & Observabilidad',
    descripcion: 'Trazabilidad X-Correlation-ID y control FinOps por usuario'
  }
];

/**
 * Vista de inicio de sesión Split-Screen con hero corporativo Ofima,
 * formulario reactivo y accesos rápidos de demostración.
 */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, ThemeCustomizerComponent, OfimaLogoComponent],
  template: `
    <div class="login-split">
      <aside class="login-hero" aria-label="Presentación corporativa Ofima MesaAyuda IA">
        <div class="hero-inner anim-fade-up">
          <div class="hero-brand">
            <app-ofima-logo variant="full" />
          </div>

          <div class="hero-copy">
            <h1>Soporte ERP inteligente con gobernanza Human-in-the-Loop</h1>
            <p class="hero-lead">
              Atención asistida por IA sobre manuales oficiales Ofima ERP, trazabilidad de extremo a extremo
              y aprobación humana obligatoria en cada publicación.
            </p>
          </div>

          <div class="hero-cards">
            @for (item of highlights; track item.titulo) {
              <article class="hero-card">
                <span class="hero-card-icon" aria-hidden="true">{{ item.icon }}</span>
                <div>
                  <strong>{{ item.titulo }}</strong>
                  <p>{{ item.descripcion }}</p>
                </div>
              </article>
            }
          </div>
        </div>
      </aside>

      <section class="login-panel" aria-label="Formulario de ingreso">
        <header class="login-topbar">
          <app-theme-customizer />
        </header>

        <main class="login-form-shell anim-fade-up">
          <header class="panel-header">
            <span class="badge badge-ia">Portal de Soporte Ofima ERP</span>
            <h2>Iniciar Sesión</h2>
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

            <button type="submit" class="btn btn-primary login-submit-btn" [disabled]="loading()">
              @if (loading()) {
                <span class="spinner"></span> Iniciando sesión...
              } @else {
                Ingresar al Portal
              }
            </button>
          </form>

          <section class="demo-section" aria-label="Acceso rápido de demostración">
            <div class="demo-heading">Acceso Rápido Demo (1 clic · clave Ofima2026*)</div>
            <div class="demo-grid">
              @for (user of demoUsers(); track user.email) {
                <button
                  type="button"
                  class="demo-card-btn"
                  [disabled]="loading()"
                  (click)="quickLogin(user)"
                >
                  <span
                    class="demo-avatar"
                    [class.avatar-analyst]="user.rol === 'Analista'"
                    aria-hidden="true"
                  >
                    {{ getInitials(user.nombre) }}
                  </span>

                  <span class="demo-info">
                    <strong class="demo-name">{{ getDisplayName(user.nombre) }}</strong>
                    <small class="demo-role-sub">{{ getAreaLabel(user) }}</small>
                  </span>

                  <span
                    class="role-pill"
                    [class.role-analyst]="user.rol === 'Analista'"
                    [class.role-client]="user.rol === 'Cliente'"
                  >
                    {{ user.rol }}
                  </span>
                </button>
              }
            </div>
          </section>
        </main>
      </section>
    </div>
  `,
  styles: [
    `
      .login-split {
        min-height: 100vh;
        display: grid;
        grid-template-columns: 1fr 1fr;
        background: var(--bg-surface);
      }

      /* Panel Izquierdo: Hero de Marca Ofima */
      .login-hero {
        position: relative;
        display: grid;
        align-items: center;
        padding: 3rem clamp(2rem, 5vw, 4.5rem);
        background: linear-gradient(145deg, #0b132b 0%, #172554 55%, var(--primary, #0052cc) 145%);
        color: #f8fafc;
        overflow: hidden;
      }

      .login-hero::after {
        content: '';
        position: absolute;
        inset: auto -18% -24% auto;
        width: 420px;
        height: 420px;
        border-radius: 50%;
        background: radial-gradient(circle, rgba(0, 149, 255, 0.24) 0%, transparent 70%);
        pointer-events: none;
      }

      .hero-inner {
        position: relative;
        z-index: 1;
        max-width: 540px;
        margin: 0 auto;
        display: grid;
        gap: 2rem;
      }

      .hero-brand {
        --logo-wordmark: #ffffff;
        --text-secondary: rgba(226, 232, 240, 0.88);
        --accent-soft: rgba(0, 149, 255, 0.22);
        --accent-text: #bae6fd;
      }

      .hero-copy {
        display: grid;
        gap: 0.75rem;

        h1 {
          margin: 0;
          font-size: clamp(1.7rem, 2.6vw, 2.35rem);
          line-height: 1.2;
          letter-spacing: -0.02em;
          color: #ffffff;
        }
      }

      .hero-lead {
        margin: 0;
        font-size: 0.96rem;
        line-height: 1.6;
        color: rgba(226, 232, 240, 0.88);
      }

      .hero-cards {
        display: grid;
        gap: 0.85rem;
      }

      .hero-card {
        display: flex;
        align-items: flex-start;
        gap: 0.85rem;
        padding: 0.95rem 1.1rem;
        border-radius: calc(var(--radius-card) * 1.1);
        background: rgba(255, 255, 255, 0.08);
        border: 1px solid rgba(255, 255, 255, 0.16);
        backdrop-filter: blur(8px);

        strong {
          display: block;
          font-size: 0.92rem;
          color: #ffffff;
          margin-bottom: 0.15rem;
        }

        p {
          margin: 0;
          font-size: 0.82rem;
          line-height: 1.45;
          color: rgba(226, 232, 240, 0.84);
        }
      }

      .hero-card-icon {
        font-size: 1.2rem;
        line-height: 1.2;
        flex-shrink: 0;
      }

      /* Panel Derecho: Formulario limpio */
      .login-panel {
        position: relative;
        display: flex;
        flex-direction: column;
        background: var(--bg-surface);
        padding: 1.25rem 1.75rem 2.5rem;
      }

      .login-topbar {
        position: relative;
        z-index: 100;
        display: flex;
        justify-content: flex-end;
        align-items: center;
        overflow: visible;
      }

      .login-form-shell {
        width: 100%;
        max-width: 430px;
        margin: auto;
        padding: 1.25rem 0;
        display: grid;
        gap: 1.25rem;
      }

      .panel-header {
        display: grid;
        gap: 0.4rem;
        justify-items: start;

        h2 {
          margin: 0.2rem 0 0;
          font-size: 1.6rem;
          letter-spacing: -0.02em;
        }

        p {
          margin: 0;
          color: var(--text-secondary);
          font-size: 0.9rem;
        }
      }

      .login-form {
        display: grid;
        gap: 0.95rem;
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

      .login-submit-btn {
        width: 100%;
        padding: 0.7rem 1rem;
        font-size: 0.94rem;
        background: var(--primary, var(--accent-primary));
        border-color: var(--primary, var(--accent-primary));
        color: #ffffff;
      }

      .demo-section {
        padding-top: 1.1rem;
        border-top: 1px solid var(--border-color);
      }

      .demo-heading {
        font-size: 0.78rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.03em;
        color: var(--text-secondary);
        margin-bottom: 0.7rem;
      }

      .demo-grid {
        display: grid;
        gap: 0.55rem;
      }

      .demo-card-btn {
        width: 100%;
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.6rem 0.8rem;
        border-radius: calc(var(--radius-card) * 0.85);
        border: 1px solid var(--border-color);
        background: var(--bg-surface-alt);
        cursor: pointer;
        text-align: left;
        transition:
          border-color 180ms ease,
          background-color 180ms ease,
          transform 180ms cubic-bezier(0.16, 1, 0.3, 1);

        &:hover:not(:disabled) {
          border-color: var(--primary, var(--accent-primary));
          background: var(--accent-soft);
          transform: translateY(-1px);
        }

        &:disabled {
          opacity: 0.6;
          cursor: not-allowed;
        }
      }

      .demo-avatar {
        width: 36px;
        height: 36px;
        border-radius: 50%;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        font-size: 0.78rem;
        font-weight: 800;
        letter-spacing: 0.02em;
        flex-shrink: 0;
        background: var(--estado-abierto-bg);
        color: var(--estado-abierto-text);
      }

      .demo-avatar.avatar-analyst {
        background: var(--accent-soft);
        color: var(--accent-text);
        border: 1px solid var(--primary, var(--accent-primary));
      }

      .demo-info {
        display: grid;
        gap: 0.08rem;
        flex: 1;
        min-width: 0;
      }

      .demo-name {
        font-size: 0.86rem;
        color: var(--text-primary);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .demo-role-sub {
        font-size: 0.76rem;
        color: var(--text-secondary);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .role-pill {
        display: inline-flex;
        align-items: center;
        padding: 0.2rem 0.6rem;
        border-radius: 999px;
        font-size: 0.72rem;
        font-weight: 700;
        flex-shrink: 0;
      }

      .role-analyst {
        background: var(--accent-soft);
        color: var(--accent-text);
      }

      .role-client {
        background: var(--success-bg);
        color: var(--success-text);
      }

      .idle-notice {
        padding: 0.75rem 0.9rem;
        border-radius: calc(var(--radius-card) * 0.75);
        background: var(--warn-bg);
        color: var(--warn-text);
        border: 1px solid var(--border-color);
        font-size: 0.85rem;
        font-weight: 500;
      }

      @media (max-width: 959px) {
        .login-split {
          grid-template-columns: 1fr;
        }

        .login-hero {
          padding: 2rem 1.5rem;
        }

        .hero-inner {
          gap: 1.35rem;
        }

        .login-panel {
          padding: 1rem 1.25rem 2rem;
        }
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

  readonly highlights = HERO_HIGHLIGHTS;
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

  getDisplayName(fullName: string): string {
    const parts = fullName.split(' - ');
    return parts[0]?.trim() || fullName;
  }

  getAreaLabel(user: DemoUser): string {
    const parts = user.nombre.split(' - ');
    if (parts.length > 1 && parts[1].trim()) {
      return parts.slice(1).join(' - ').trim();
    }
    const defaults: Record<RolUsuario, string> = {
      Analista: 'Analista de Soporte ERP',
      Cliente: 'Usuario Empresarial ERP'
    };
    return defaults[user.rol];
  }

  getInitials(fullName: string): string {
    const baseName = this.getDisplayName(fullName);
    const tokens = baseName.split(/\s+/).filter(Boolean);
    if (tokens.length === 0) {
      return 'OF';
    }
    if (tokens.length === 1) {
      return tokens[0].slice(0, 2).toUpperCase();
    }
    return `${tokens[0][0]}${tokens[1][0]}`.toUpperCase();
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
