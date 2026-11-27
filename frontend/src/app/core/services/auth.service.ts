import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { DemoUser, LoginRequest, LoginResponse } from '../models/api.models';

const SESSION_STORAGE_KEY = 'mesaayuda_session_v1';

/**
 * Servicio de autenticación JWT y estado de sesión mediante Angular Signals.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly currentUser = signal<LoginResponse | null>(this.loadSession());
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly isAnalista = computed(() => this.currentUser()?.rol === 'Analista');
  readonly isCliente = computed(() => this.currentUser()?.rol === 'Cliente');

  /** Autentica al usuario contra POST /api/auth/login y persiste la sesión JWT. */
  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', request).pipe(
      tap((response) => {
        this.currentUser.set(response);
        this.persistSession(response);
      })
    );
  }

  /** Consulta los usuarios de demostración registrados en GET /api/auth/demo-users. */
  getDemoUsers(): Observable<DemoUser[]> {
    return this.http.get<DemoUser[]>('/api/auth/demo-users');
  }

  /** Cierra la sesión actual, limpia localStorage y redirige a /login. */
  logout(): void {
    this.currentUser.set(null);
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem(SESSION_STORAGE_KEY);
    }
    void this.router.navigate(['/login']);
  }

  /** Retorna el token JWT vigente o null si no hay sesión activa. */
  getToken(): string | null {
    return this.currentUser()?.token ?? null;
  }

  private loadSession(): LoginResponse | null {
    if (typeof localStorage === 'undefined') {
      return null;
    }
    try {
      const raw = localStorage.getItem(SESSION_STORAGE_KEY);
      if (!raw) {
        return null;
      }
      const parsed = JSON.parse(raw) as Partial<LoginResponse>;
      if (parsed.token && parsed.email && parsed.rol) {
        return parsed as LoginResponse;
      }
      return null;
    } catch {
      return null;
    }
  }

  private persistSession(session: LoginResponse): void {
    if (typeof localStorage === 'undefined') {
      return;
    }
    try {
      localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Ignora fallos de cuota en almacenamiento local.
    }
  }
}
