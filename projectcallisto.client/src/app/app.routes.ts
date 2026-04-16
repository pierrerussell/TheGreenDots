import { Routes } from '@angular/router';
import { authGuard } from '../auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/home/home.component').then(m => m.HomeComponent),
    canActivate: [authGuard],
  },
  {
    path: 'onboarding/add-organization',
    loadComponent: () =>
      import('./pages/onboarding/add-organization.component').then(m => m.AddOrganizationComponent),
    canActivate: [authGuard],
  },
  {
    path: 'organisation/:id',
    loadComponent: () =>
      import('./pages/organisation/organisation.component').then(m => m.OrganisationComponent),
    canActivate: [authGuard],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
