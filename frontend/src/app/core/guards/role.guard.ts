import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { RolUsuario } from '../models/api.models';
import { AuthService } from '../services/auth.service';

/**
 * Guard funcional que restringe el acceso a rutas según los roles definidos en route.data['roles'].
 */
export const roleGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const user = authService.currentUser();
  if (!user) {
    return router.createUrlTree(['/login']);
  }

  const allowedRoles = route.data['roles'] as RolUsuario[] | undefined;
  if (!allowedRoles || allowedRoles.includes(user.rol)) {
    return true;
  }

  return router.createUrlTree(['/casos']);
};
