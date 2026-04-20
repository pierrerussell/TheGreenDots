import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { OrganisationService } from '../organisation.service';

interface TeamMember {
  id: string;
  displayName: string;
  email: string | null;
  jobTitle: string | null;
  isAssignedSeat: boolean;
}

interface Subscription {
  id: string;
  status: string;
  paidSeats: number;
  assignedSeats: number;
  trialEndsAt: string | null;
  stripeCustomerId: string | null;
  stripeSubscriptionId: string | null;
  createdAt: string;
}

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './settings.component.html',
})
export class SettingsComponent implements OnInit {
  private router = inject(Router);
  private http = inject(HttpClient);
  orgService = inject(OrganisationService);

  editingName = signal(false);
  newName = signal('');
  savingName = signal(false);

  showDeleteConfirm = signal(false);
  deleteConfirmText = signal('');
  deleting = signal(false);

  // Seat management
  allMembers = signal<TeamMember[]>([]);
  subscription = signal<Subscription | null>(null);
  loadingSeats = signal(true);
  togglingSeats = signal<Set<string>>(new Set());

  ngOnInit(): void {
    this.loadSeatManagement();
  }

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

  async loadSeatManagement(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org) return;

    this.loadingSeats.set(true);
    try {
      const [members, sub] = await Promise.all([
        this.http.get<TeamMember[]>(`/api/organisations/${org.id}/all-members`).toPromise(),
        this.http.get<Subscription>(`/api/organisations/${org.id}/subscription`).toPromise(),
      ]);

      this.allMembers.set(members || []);
      this.subscription.set(sub || null);
    } catch (err) {
      console.error('Failed to load seat management', err);
    } finally {
      this.loadingSeats.set(false);
    }
  }

  async toggleSeat(member: TeamMember): Promise<void> {
    const org = this.orgService.organisation();
    const sub = this.subscription();
    if (!org || !sub) return;

    // Prevent toggling if already in progress
    if (this.togglingSeats().has(member.id)) return;

    // Check if we can assign (seat limit reached and member not assigned)
    if (!member.isAssignedSeat && sub.assignedSeats >= sub.paidSeats) {
      return;
    }

    // Add to toggling set
    const toggling = new Set(this.togglingSeats());
    toggling.add(member.id);
    this.togglingSeats.set(toggling);

    try {
      const endpoint = member.isAssignedSeat
        ? `/api/organisations/${org.id}/members/${member.id}/unassign-seat`
        : `/api/organisations/${org.id}/members/${member.id}/assign-seat`;

      await this.http.post(endpoint, {}).toPromise();

      // Update local state
      const updatedMembers = this.allMembers().map((m) =>
        m.id === member.id ? { ...m, isAssignedSeat: !m.isAssignedSeat } : m
      );
      this.allMembers.set(updatedMembers);

      // Update subscription assigned count
      const newAssignedCount = member.isAssignedSeat
        ? sub.assignedSeats - 1
        : sub.assignedSeats + 1;
      this.subscription.set({ ...sub, assignedSeats: newAssignedCount });
    } catch (err) {
      console.error('Failed to toggle seat', err);
    } finally {
      // Remove from toggling set
      const toggling = new Set(this.togglingSeats());
      toggling.delete(member.id);
      this.togglingSeats.set(toggling);
    }
  }

  canToggleSeat(member: TeamMember): boolean {
    const sub = this.subscription();
    if (!sub) return false;

    // Can always unassign
    if (member.isAssignedSeat) return true;

    // Can assign if seats available
    return sub.assignedSeats < sub.paidSeats;
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
