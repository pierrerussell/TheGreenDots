import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet, ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { OrganisationService } from './organisation.service';

@Component({
  selector: 'app-organisation-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="min-h-screen bg-surface-100 flex">
      <!-- Sidebar -->
      <aside class="w-60 bg-white border-r border-surface-200 flex flex-col fixed inset-y-0 left-0 z-20">
        <!-- Header -->
        <div class="h-16 px-4 border-b border-surface-200 flex items-center gap-3">
          <button
            (click)="goHome()"
            class="p-1.5 -ml-1 rounded-lg text-surface-400 hover:text-surface-600 hover:bg-surface-100 transition-colors"
          >
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 19.5 3 12m0 0 7.5-7.5M3 12h18" />
            </svg>
          </button>
          <div class="flex-1 min-w-0">
            @if (orgService.organisation()) {
              <h1 class="text-sm font-semibold text-surface-900 truncate">{{ orgService.organisation()!.name }}</h1>
              <p class="text-xs text-surface-500">Organisation</p>
            } @else {
              <div class="h-4 w-24 bg-surface-100 rounded animate-pulse"></div>
            }
          </div>
        </div>

        <!-- Navigation -->
        <nav class="flex-1 p-3 space-y-1">
          <a
            [routerLink]="['/organisation', orgId, 'overview']"
            routerLinkActive="bg-green-50 text-green-700 border-green-200"
            class="flex items-center gap-3 px-3 py-2 text-sm font-medium rounded-lg border border-transparent text-surface-600 hover:bg-surface-50 hover:text-surface-900 transition-colors"
          >
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 6A2.25 2.25 0 0 1 6 3.75h2.25A2.25 2.25 0 0 1 10.5 6v2.25a2.25 2.25 0 0 1-2.25 2.25H6a2.25 2.25 0 0 1-2.25-2.25V6ZM3.75 15.75A2.25 2.25 0 0 1 6 13.5h2.25a2.25 2.25 0 0 1 2.25 2.25V18a2.25 2.25 0 0 1-2.25 2.25H6A2.25 2.25 0 0 1 3.75 18v-2.25ZM13.5 6a2.25 2.25 0 0 1 2.25-2.25H18A2.25 2.25 0 0 1 20.25 6v2.25A2.25 2.25 0 0 1 18 10.5h-2.25a2.25 2.25 0 0 1-2.25-2.25V6ZM13.5 15.75a2.25 2.25 0 0 1 2.25-2.25H18a2.25 2.25 0 0 1 2.25 2.25V18A2.25 2.25 0 0 1 18 20.25h-2.25A2.25 2.25 0 0 1 13.5 18v-2.25Z" />
            </svg>
            Overview
          </a>

          <a
            [routerLink]="['/organisation', orgId, 'people']"
            routerLinkActive="bg-green-50 text-green-700 border-green-200"
            class="flex items-center gap-3 px-3 py-2 text-sm font-medium rounded-lg border border-transparent text-surface-600 hover:bg-surface-50 hover:text-surface-900 transition-colors"
          >
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" />
            </svg>
            People
          </a>

          <a
            [routerLink]="['/organisation', orgId, 'reports']"
            routerLinkActive="bg-green-50 text-green-700 border-green-200"
            class="flex items-center gap-3 px-3 py-2 text-sm font-medium rounded-lg border border-transparent text-surface-600 hover:bg-surface-50 hover:text-surface-900 transition-colors"
          >
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" d="M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 0 1 3 19.875v-6.75ZM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V8.625ZM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 3.504 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V4.125Z" />
            </svg>
            Reports
          </a>

          <div class="pt-4 mt-4 border-t border-surface-100">
            <p class="px-3 mb-2 text-xs font-semibold text-surface-400 uppercase tracking-wider">Admin</p>

            <a
              [routerLink]="['/organisation', orgId, 'settings']"
              routerLinkActive="bg-green-50 text-green-700 border-green-200"
              class="flex items-center gap-3 px-3 py-2 text-sm font-medium rounded-lg border border-transparent text-surface-600 hover:bg-surface-50 hover:text-surface-900 transition-colors"
            >
              <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" d="M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.325.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 0 1 1.37.49l1.296 2.247a1.125 1.125 0 0 1-.26 1.431l-1.003.827c-.293.241-.438.613-.43.992a7.723 7.723 0 0 1 0 .255c-.008.378.137.75.43.991l1.004.827c.424.35.534.955.26 1.43l-1.298 2.247a1.125 1.125 0 0 1-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.47 6.47 0 0 1-.22.128c-.331.183-.581.495-.644.869l-.213 1.281c-.09.543-.56.94-1.11.94h-2.594c-.55 0-1.019-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 0 1-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 0 1-1.369-.49l-1.297-2.247a1.125 1.125 0 0 1 .26-1.431l1.004-.827c.292-.24.437-.613.43-.991a6.932 6.932 0 0 1 0-.255c.007-.38-.138-.751-.43-.992l-1.004-.827a1.125 1.125 0 0 1-.26-1.43l1.297-2.247a1.125 1.125 0 0 1 1.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.086.22-.128.332-.183.582-.495.644-.869l.214-1.28Z" />
                <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" />
              </svg>
              Settings
            </a>

            <a
              [routerLink]="['/organisation', orgId, 'email-reports']"
              routerLinkActive="bg-green-50 text-green-700 border-green-200"
              class="flex items-center gap-3 px-3 py-2 text-sm font-medium rounded-lg border border-transparent text-surface-600 hover:bg-surface-50 hover:text-surface-900 transition-colors"
            >
              <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" d="M21.75 6.75v10.5a2.25 2.25 0 0 1-2.25 2.25h-15a2.25 2.25 0 0 1-2.25-2.25V6.75m19.5 0A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25m19.5 0v.243a2.25 2.25 0 0 1-1.07 1.916l-7.5 4.615a2.25 2.25 0 0 1-2.36 0L3.32 8.91a2.25 2.25 0 0 1-1.07-1.916V6.75" />
              </svg>
              Email Reports
            </a>

            <a
              [routerLink]="['/organisation', orgId, 'subscription']"
              routerLinkActive="bg-green-50 text-green-700 border-green-200"
              class="flex items-center gap-3 px-3 py-2 text-sm font-medium rounded-lg border border-transparent text-surface-600 hover:bg-surface-50 hover:text-surface-900 transition-colors"
            >
              <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 8.25h19.5M2.25 9h19.5m-16.5 5.25h6m-6 2.25h3m-3.75 3h15a2.25 2.25 0 0 0 2.25-2.25V6.75A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25v10.5A2.25 2.25 0 0 0 4.5 19.5Z" />
              </svg>
              Subscription
            </a>
          </div>
        </nav>

        <!-- Footer -->
        <div class="p-3 border-t border-surface-200">
          <div class="flex items-center gap-3 px-3 py-2">
            <div class="flex items-center gap-1">
              <span class="w-2 h-2 rounded-full bg-green-500 animate-pulse-soft"></span>
              <span class="w-2 h-2 rounded-full bg-green-400 animate-pulse-soft" style="animation-delay: 0.5s;"></span>
            </div>
            <span class="text-xs font-medium text-surface-500">The Green Dots</span>
          </div>
        </div>
      </aside>

      <!-- Main content -->
      <main class="flex-1 ml-60">
        @if (orgService.loading()) {
          <div class="flex items-center justify-center h-screen">
            <div class="flex flex-col items-center">
              <div class="relative w-10 h-10 mb-4">
                <div class="absolute inset-0 rounded-full border-2 border-surface-200"></div>
                <div class="absolute inset-0 rounded-full border-2 border-green-500 border-t-transparent animate-spin"></div>
              </div>
              <p class="text-surface-500 text-sm">Loading organisation...</p>
            </div>
          </div>
        } @else if (orgService.organisation()) {
          <router-outlet></router-outlet>
        }
      </main>
    </div>
  `,
})
export class OrganisationLayoutComponent implements OnInit {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  orgService = inject(OrganisationService);

  orgId = '';

  ngOnInit(): void {
    this.orgId = this.route.snapshot.paramMap.get('id') || '';
    if (this.orgId) {
      this.orgService.loadOrganisation(this.orgId);
    }
  }

  goHome(): void {
    this.router.navigate(['/']);
  }
}
