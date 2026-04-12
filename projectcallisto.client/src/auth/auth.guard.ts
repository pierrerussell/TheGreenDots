import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { AuthService } from './auth.service';
import { map } from 'rxjs';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  return authService.checkAuth().pipe(
    map(isAuthenticated => {
      if (isAuthenticated) {
        return true;
      }
      const returnUrl = encodeURIComponent(state.url);
      window.location.href = `/signin?returnUrl=${returnUrl}`;
      return false;
    }
  ));
}
