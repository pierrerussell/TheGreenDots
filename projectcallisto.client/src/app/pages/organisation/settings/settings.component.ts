import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { OrganisationService } from '../organisation.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './settings.component.html',
})
export class SettingsComponent {
  private router = inject(Router);
  private http = inject(HttpClient);
  orgService = inject(OrganisationService);

  editingName = signal(false);
  newName = signal('');
  savingName = signal(false);

  showDeleteConfirm = signal(false);
  deleteConfirmText = signal('');
  deleting = signal(false);

  startEditingName(): void {
    this.newName.set(this.orgService.organisation()?.name || '');
    this.editingName.set(true);
  }

  cancelEditingName(): void {
    this.editingName.set(false);
    this.newName.set('');
  }

  async saveName(): Promise<void> {
    const name = this.newName().trim();
    if (!name) return;

    this.savingName.set(true);
    try {
      await this.orgService.updateOrganisation({ name });
      this.editingName.set(false);
    } catch (err) {
      console.error('Failed to update name', err);
    } finally {
      this.savingName.set(false);
    }
  }

  async deleteOrganisation(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org || this.deleteConfirmText() !== org.name) return;

    this.deleting.set(true);
    this.http.delete(`/api/organisations/${org.id}`).subscribe({
      next: () => {
        this.orgService.clear();
        this.router.navigate(['/']);
      },
      error: (err) => {
        console.error('Failed to delete organisation', err);
        this.deleting.set(false);
      },
    });
  }
}
