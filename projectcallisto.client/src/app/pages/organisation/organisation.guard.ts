import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { catchError, map, of } from 'rxjs';

interface AccessResponse {
  hasAccess: boolean;
  role: string | null;
}

export const organisationGuard: CanActivateFn = (route) => {
  const http = inject(HttpClient);
  const router = inject(Router);

  const orgId = route.paramMap.get('id');
  if (!orgId) {
    router.navigate(['/not-found']);
    return of(false);
  }

  // Try the access endpoint first, fall back to checking if org exists
  return http.get<AccessResponse>(`/api/organisations/${orgId}/access`).pipe(
    map(response => {
      console.log('[organisationGuard] Response:', response);
      if (response.hasAccess) {
        console.log('[organisationGuard] Access granted');
        return true;
      }
      console.log('[organisationGuard] No access, redirecting to /not-found');
      router.navigate(['/not-found']);
      return false;
    }),
    catchError((error: HttpErrorResponse) => {
      console.error('[organisationGuard] Error:', error);
      // If access endpoint doesn't exist (404), try fetching the org directly
      // This is a fallback until the access endpoint is implemented
      if (error.status === 404) {
        console.log('[organisationGuard] 404, trying fallback');
        return http.get(`/api/organisations/${orgId}`).pipe(
          map(() => true), // If org exists and user can fetch it, allow access
          catchError(() => {
            router.navigate(['/not-found']);
            return of(false);
          })
        );
      }
      // For 403 or other errors, deny access
      console.log('[organisationGuard] Redirecting to /not-found due to error');
      router.navigate(['/not-found']);
      return of(false);
    })
  );
};
