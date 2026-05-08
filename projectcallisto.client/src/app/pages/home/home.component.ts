import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';

interface Organisation {
  id: string;
  name: string;
  tenantId: string;
  createdAt: string;
}

@Component({
  selector: 'app-home',
  standalone: true,
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent implements OnInit {
  private router = inject(Router);
  private http = inject(HttpClient);

  organisations = signal<Organisation[]>([]);
  loading = signal(true);
  showUserMenu = signal(false);

  ngOnInit(): void {
    this.loadOrganisations();
  }

  private loadOrganisations(): void {
    this.http.get<Organisation[]>('/api/organisations').subscribe({
      next: (orgs) => {
        this.organisations.set(orgs);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  startAddOrganization(): void {
    this.router.navigate(['/onboarding/add-organization']);
  }

  deleteOrganisation(id: string, event: Event): void {
    event.stopPropagation();
    if (confirm('Are you sure you want to delete this organisation?')) {
      this.http.delete(`/api/organisations/${id}`).subscribe({
        next: () => {
          this.organisations.update(orgs => orgs.filter(o => o.id !== id));
        },
      });
    }
  }

  viewOrganisation(id: string): void {
    this.router.navigate(['/organisation', id]);
  }

  toggleUserMenu(): void {
    this.showUserMenu.update(v => !v);
  }

  logout(): void {
    window.location.href = '/signout';
  }
}
