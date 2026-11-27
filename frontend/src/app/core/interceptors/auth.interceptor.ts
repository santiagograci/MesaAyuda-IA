import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

function generateCorrelationId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `fe-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

/**
 * Interceptor funcional que adjunta el token Bearer y `X-Correlation-ID` en cada petición API,
 * y cierra sesión automáticamente ante respuestas 401 Unauthorized.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getToken();

  const headers: Record<string, string> = {};
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }
  if (!req.headers.has('X-Correlation-ID')) {
    headers['X-Correlation-ID'] = generateCorrelationId();
  }

  const authReq = Object.keys(headers).length > 0 ? req.clone({ setHeaders: headers }) : req;

  return next(authReq).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !req.url.includes('/api/auth/login')
      ) {
        authService.logout();
      }
      return throwError(() => error);
    })
  );
};
