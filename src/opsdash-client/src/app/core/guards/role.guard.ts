import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const expected = route.data['roles'] as string[] | undefined;
  const role = auth.currentUser()?.role;

  if (!expected?.length || (role && expected.includes(role))) {
    return true;
  }

  return router.createUrlTree(['/dashboard']);
};
