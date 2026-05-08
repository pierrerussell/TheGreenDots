import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { OrganisationService } from '../organisation.service';
import { ToastService } from '../../../shared/toast.service';

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

interface WorkingHours {
  id: string | null;
  startTime: string; // "HH:mm:ss"
  endTime: string; // "HH:mm:ss"
  workingDays: string[]; // ["monday", "tuesday", etc.]
}

interface TimezoneInfo {
  timezone: string;
  timezoneDetectedFrom: string;
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
  private toast = inject(ToastService);
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

  // Working hours
  workingHours = signal<WorkingHours | null>(null);
  loadingWorkingHours = signal(true);
  savingWorkingHours = signal(false);
  workingDaysOptions = [
    { value: 'monday', label: 'Monday' },
    { value: 'tuesday', label: 'Tuesday' },
    { value: 'wednesday', label: 'Wednesday' },
    { value: 'thursday', label: 'Thursday' },
    { value: 'friday', label: 'Friday' },
    { value: 'saturday', label: 'Saturday' },
    { value: 'sunday', label: 'Sunday' },
  ];

  // Timezone
  timezoneInfo = signal<TimezoneInfo | null>(null);
  loadingTimezone = signal(true);
  editingTimezone = signal(false);
  selectedTimezone = signal('');
  savingTimezone = signal(false);

  // Common timezones for quick selection
  commonTimezones = [
    { value: 'UTC', label: 'UTC (Coordinated Universal Time)' },
    { value: 'America/New_York', label: 'Eastern Time (US & Canada)' },
    { value: 'America/Chicago', label: 'Central Time (US & Canada)' },
    { value: 'America/Denver', label: 'Mountain Time (US & Canada)' },
    { value: 'America/Los_Angeles', label: 'Pacific Time (US & Canada)' },
    { value: 'Europe/London', label: 'London (GMT/BST)' },
    { value: 'Europe/Paris', label: 'Paris (CET/CEST)' },
    { value: 'Asia/Tokyo', label: 'Tokyo (JST)' },
    { value: 'Asia/Shanghai', label: 'Shanghai (CST)' },
    { value: 'Asia/Singapore', label: 'Singapore (SGT)' },
    { value: 'Asia/Dubai', label: 'Dubai (GST)' },
    { value: 'Australia/Sydney', label: 'Sydney (AEDT/AEST)' },
  ];

  ngOnInit(): void {
    this.loadSeatManagement();
    this.loadWorkingHours();
    this.loadTimezone();
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
      this.toast.success('Organisation name updated successfully');
    } catch (err) {
      console.error('Failed to update name', err);
      this.toast.error('Failed to update organisation name. Please try again.');
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
        this.toast.success('Organisation deleted successfully');
        this.orgService.clear();
        this.router.navigate(['/']);
      },
      error: (err) => {
        console.error('Failed to delete organisation', err);
        this.toast.error('Failed to delete organisation. Please try again.');
        this.deleting.set(false);
      },
    });
  }

  async loadWorkingHours(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org) return;

    this.loadingWorkingHours.set(true);
    try {
      const wh = await this.http
        .get<WorkingHours>(`/api/organisations/${org.id}/working-hours`)
        .toPromise();
      this.workingHours.set(wh || null);
    } catch (err) {
      console.error('Failed to load working hours', err);
    } finally {
      this.loadingWorkingHours.set(false);
    }
  }

  async saveWorkingHours(): Promise<void> {
    const org = this.orgService.organisation();
    const wh = this.workingHours();
    if (!org || !wh) return;

    if (wh.workingDays.length === 0) {
      this.toast.error('Please select at least one working day');
      return;
    }

    this.savingWorkingHours.set(true);
    try {
      const payload = {
        startTime: wh.startTime,
        endTime: wh.endTime,
        workingDays: wh.workingDays,
      };

      const updated = await this.http
        .put<WorkingHours>(`/api/organisations/${org.id}/working-hours`, payload)
        .toPromise();

      this.workingHours.set(updated || null);
    } catch (err) {
      console.error('Failed to save working hours', err);
      this.toast.error('Failed to save working hours');
    } finally {
      this.savingWorkingHours.set(false);
    }
  }

  toggleWorkingDay(day: string): void {
    const wh = this.workingHours();
    if (!wh) return;

    const days = new Set(wh.workingDays);
    if (days.has(day)) {
      days.delete(day);
    } else {
      days.add(day);
    }

    this.workingHours.set({
      ...wh,
      workingDays: Array.from(days),
    });
  }

  isWorkingDaySelected(day: string): boolean {
    return this.workingHours()?.workingDays.includes(day) ?? false;
  }

  updateStartTime(time: string): void {
    const wh = this.workingHours();
    if (!wh) return;

    this.workingHours.set({
      ...wh,
      startTime: time + ':00', // Convert "HH:mm" to "HH:mm:ss"
    });
  }

  updateEndTime(time: string): void {
    const wh = this.workingHours();
    if (!wh) return;

    this.workingHours.set({
      ...wh,
      endTime: time + ':00', // Convert "HH:mm" to "HH:mm:ss"
    });
  }

  getTimeValue(timeString: string): string {
    // Convert "HH:mm:ss" to "HH:mm" for input[type="time"]
    return timeString.substring(0, 5);
  }

  // Timezone management
  async loadTimezone(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org) return;

    this.loadingTimezone.set(true);
    try {
      const data = await this.http
        .get<TimezoneInfo>(`/api/organisations/${org.id}/timezone`)
        .toPromise();

      this.timezoneInfo.set(data || null);
    } catch (err) {
      console.error('Failed to load timezone', err);
    } finally {
      this.loadingTimezone.set(false);
    }
  }

  startEditingTimezone(): void {
    const current = this.timezoneInfo();
    this.selectedTimezone.set(current?.timezone || 'UTC');
    this.editingTimezone.set(true);
  }

  cancelEditingTimezone(): void {
    this.editingTimezone.set(false);
    this.selectedTimezone.set('');
  }

  async saveTimezone(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org) return;

    this.savingTimezone.set(true);
    try {
      const updated = await this.http
        .put<TimezoneInfo>(`/api/organisations/${org.id}/timezone`, {
          timezone: this.selectedTimezone()
        })
        .toPromise();

      this.timezoneInfo.set(updated || null);
      this.editingTimezone.set(false);
      this.toast.success('Timezone updated successfully! Email reports will now use the correct timezone.');
    } catch (err) {
      console.error('Failed to save timezone', err);
      this.toast.error('Failed to save timezone. Please try again.');
    } finally {
      this.savingTimezone.set(false);
    }
  }
}
