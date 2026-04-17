import { Component, inject, signal } from '@angular/core';
import { OrganisationService } from '../organisation.service';

@Component({
  selector: 'app-people',
  standalone: true,
  templateUrl: './people.component.html',
})
export class PeopleComponent {
  orgService = inject(OrganisationService);

  syncing = signal(false);
  syncSuccess = signal(false);
  syncError = signal<string | null>(null);

  async syncFromMicrosoft(): Promise<void> {
    this.syncing.set(true);
    this.syncSuccess.set(false);
    this.syncError.set(null);

    try {
      await this.orgService.syncMembers();
      this.syncSuccess.set(true);
      setTimeout(() => this.syncSuccess.set(false), 3000);
    } catch (err) {
      this.syncError.set('Failed to sync members. Please try again.');
      setTimeout(() => this.syncError.set(null), 5000);
    } finally {
      this.syncing.set(false);
    }
  }
}
