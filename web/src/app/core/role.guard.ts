import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role } from './api.types';
import { AuthService } from './auth.service';

/** Allows the route only to the given roles. Signed-out users go to login; users with the wrong role go to their own home. */
export const roleGuard =
  (...roles: Role[]): CanActivateFn =>
  () => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated()) {
      auth.logout();   // drop an expired session
      return router.createUrlTree(['/login']);
    }

    return auth.hasRole(...roles) ? true : router.createUrlTree([auth.homePath()]);
  };

/** For the root path: send everyone to the right place. */
export const homeRedirect: CanActivateFn = () => {
  const auth = inject(AuthService);
  return inject(Router).createUrlTree([auth.isAuthenticated() ? auth.homePath() : '/login']);
};
