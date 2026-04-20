import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { catchError, map, of } from 'rxjs';

interface AccessResponse {
  hasAccess: boolean;
  role: string | null;
}

console.log('[adminGuard] File loaded - NEW VERSION v2');

export const adminGuard: CanActivateFn = (route) => {
  console.log('[adminGuard] Guard executing!!!');
  const http = inject(HttpClient);
  const router = inject(Router);

  // Get org ID from parent route since settings is a child route
  const orgId = route.parent?.paramMap.get('id');
  console.log('[adminGuard] Org ID from parent:', orgId);
  if (!orgId) {
    console.log('[adminGuard] No org ID found, redirecting to /not-found');
    router.navigate(['/not-found']);
    return of(false);
  }

  return http.get<AccessResponse>(`/api/organisations/${orgId}/access`).pipe(
    map(response => {
      console.log('[adminGuard] Response:', response);
      if (response.hasAccess && response.role === 'admin') {
        console.log('[adminGuard] User is admin, allowing access');
        return true;
      }
      // User has access but is not admin - redirect to overview
      console.log('[adminGuard] User is not admin, redirecting to overview');
      router.navigate(['/organisation', orgId, 'overview']);
      return false;
    }),
    catchError((error) => {
      // Access check failed - redirect to overview
      console.error('[adminGuard] Error:', error);
      router.navigate(['/organisation', orgId, 'overview']);
      return of(false);
    })
  );
};
