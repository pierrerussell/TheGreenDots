import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-home',
  standalone: true,
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {
  private router = inject(Router);

  startAddOrganization(): void {
    this.router.navigate(['/onboarding/add-organization']);
  }
}
