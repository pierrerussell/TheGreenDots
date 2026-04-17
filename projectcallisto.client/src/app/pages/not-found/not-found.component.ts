import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="min-h-screen bg-surface-100 flex items-center justify-center p-6">
      <div class="max-w-sm text-center animate-fade-up">
        <div class="w-16 h-16 rounded-2xl bg-surface-200 flex items-center justify-center mx-auto mb-6">
          <svg class="w-8 h-8 text-surface-400" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z" />
          </svg>
        </div>

        <h1 class="text-2xl font-semibold text-surface-900 mb-2">Page not found</h1>
        <p class="text-surface-500 mb-8">
          The page you're looking for doesn't exist or you don't have permission to view it.
        </p>

        <a
          routerLink="/"
          class="inline-flex items-center gap-2 px-5 py-2.5 bg-green-600 hover:bg-green-700 text-white font-semibold rounded-lg transition-colors"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 19.5 3 12m0 0 7.5-7.5M3 12h18" />
          </svg>
          Back to Home
        </a>
      </div>
    </div>
  `,
})
export class NotFoundComponent {}
