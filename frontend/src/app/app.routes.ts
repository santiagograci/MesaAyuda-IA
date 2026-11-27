import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

/**
 * Definición de rutas principales con carga perezosa (lazy loading) de componentes Standalone.
 */
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'casos',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/casos/casos-shell.component').then((m) => m.CasosShellComponent)
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'casos'
  },
  {
    path: '**',
    redirectTo: 'casos'
  }
];
