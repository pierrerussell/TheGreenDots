import { Routes } from '@angular/router';
import { authGuard } from '../auth/auth.guard';
import { organisationGuard } from './pages/organisation/organisation.guard';
import { adminGuard } from './pages/organisation/admin.guard';

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
      import('./pages/organisation/organisation-layout.component').then(m => m.OrganisationLayoutComponent),
    canActivate: [authGuard, organisationGuard],
    children: [
      {
        path: '',
        redirectTo: 'overview',
        pathMatch: 'full',
      },
      {
        path: 'overview',
        loadComponent: () =>
          import('./pages/organisation/overview/overview.component').then(m => m.OverviewComponent),
      },
      {
        path: 'people',
        loadComponent: () =>
          import('./pages/organisation/people/people.component').then(m => m.PeopleComponent),
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./pages/organisation/reports/reports.component').then(m => m.ReportsComponent),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./pages/organisation/settings/settings.component').then(m => m.SettingsComponent),
        canActivate: [adminGuard],
      },
      {
        path: 'subscription',
        loadComponent: () =>
          import('./pages/organisation/subscription/subscription.component').then(m => m.SubscriptionComponent),
        canActivate: [adminGuard],
      },
      {
        path: 'pricing',
        loadComponent: () =>
          import('./pages/organisation/subscription/pricing.component').then(m => m.PricingComponent),
        canActivate: [adminGuard],
      },
      {
        path: 'email-reports',
        loadComponent: () =>
          import('./pages/organisation/reports/email-report-settings.component').then(m => m.EmailReportSettingsComponent),
        canActivate: [adminGuard],
      },
    ],
  },
  {
    path: 'not-found',
    loadComponent: () =>
      import('./pages/not-found/not-found.component').then(m => m.NotFoundComponent),
  },
  {
    path: '**',
    redirectTo: 'not-found',
  },
];
