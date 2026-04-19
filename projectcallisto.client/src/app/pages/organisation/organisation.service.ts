import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface Organisation {
  id: string;
  name: string;
  tenantId: string;
  createdAt: string;
}

export interface TeamMember {
  id: string;
  displayName: string;
  email: string | null;
  jobTitle: string | null;
  availability: string;
  activity: string | null;
}

export interface PresenceTimelineEntry {
  memberId: string;
  memberName: string;
  entries: {
    status: string;
    startTime: string;
    endTime: string | null;
    durationMinutes: number;
  }[];
}

export interface PresenceChange {
  id: string;
  memberId: string;
  memberName: string;
  previousStatus: string;
  newStatus: string;
  timestamp: string;
}

export interface WeeklyReportSettings {
  enabled: boolean;
  recipients: string[];
  dayOfWeek: number; // 0 = Sunday, 1 = Monday, etc.
  timeUtc: string; // e.g., "09:00"
}

@Injectable({
  providedIn: 'root',
})
export class OrganisationService {
  private http = inject(HttpClient);

  // State
  organisation = signal<Organisation | null>(null);
  members = signal<TeamMember[]>([]);
  loading = signal(false);
  error = signal<string | null>(null);

  // Computed stats
  availableCount = computed(() =>
    this.members().filter(m => m.availability === 'Available').length
  );

  awayCount = computed(() =>
    this.members().filter(m => ['Away', 'BeRightBack'].includes(m.availability)).length
  );

  busyCount = computed(() =>
    this.members().filter(m => ['Busy', 'DoNotDisturb'].includes(m.availability)).length
  );

  offlineCount = computed(() =>
    this.members().filter(m => m.availability === 'Offline').length
  );

  private currentOrgId = '';

  loadOrganisation(orgId: string): void {
    if (this.currentOrgId === orgId && this.organisation()) {
      return; // Already loaded
    }

    this.currentOrgId = orgId;
    this.loading.set(true);
    this.error.set(null);

    this.http.get<Organisation>(`/api/organisations/${orgId}`).subscribe({
      next: (org) => {
        this.organisation.set(org);
        this.loadMembers(orgId);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set('Failed to load organisation');
        console.error('Failed to load organisation', err);
      },
    });
  }

  loadMembers(orgId?: string): void {
    const id = orgId || this.currentOrgId;
    this.http.get<TeamMember[]>(`/api/organisations/${id}/members`).subscribe({
      next: (members) => {
        this.members.set(members);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        console.error('Failed to load members', err);
      },
    });
  }

  syncMembers(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.http.post<TeamMember[]>(`/api/organisations/${this.currentOrgId}/members/sync`, {}).subscribe({
        next: (members) => {
          this.members.set(members);
          resolve();
        },
        error: (err) => {
          console.error('Failed to sync members', err);
          reject(err);
        },
      });
    });
  }

  getPresenceTimeline(startTime: string, endTime: string): Promise<PresenceTimelineEntry[]> {
    return new Promise((resolve, reject) => {
      this.http.get<PresenceTimelineEntry[]>(
        `/api/organisations/${this.currentOrgId}/presence-timeline`,
        { params: { startTime, endTime } }
      ).subscribe({
        next: (timeline) => resolve(timeline),
        error: (err) => {
          console.error('Failed to load timeline', err);
          reject(err);
        },
      });
    });
  }

  getPresenceHistory(date: string): Promise<PresenceChange[]> {
    return new Promise((resolve, reject) => {
      this.http.get<PresenceChange[]>(
        `/api/organisations/${this.currentOrgId}/presence-history`,
        { params: { date } }
      ).subscribe({
        next: (history) => resolve(history),
        error: (err) => {
          console.error('Failed to load history', err);
          reject(err);
        },
      });
    });
  }

  getWeeklyReportSettings(): Promise<WeeklyReportSettings> {
    return new Promise((resolve, reject) => {
      this.http.get<WeeklyReportSettings>(
        `/api/organisations/${this.currentOrgId}/weekly-report-settings`
      ).subscribe({
        next: (settings) => resolve(settings),
        error: (err) => {
          console.error('Failed to load weekly report settings', err);
          reject(err);
        },
      });
    });
  }

  updateWeeklyReportSettings(settings: WeeklyReportSettings): Promise<void> {
    return new Promise((resolve, reject) => {
      this.http.put(
        `/api/organisations/${this.currentOrgId}/weekly-report-settings`,
        settings
      ).subscribe({
        next: () => resolve(),
        error: (err) => {
          console.error('Failed to update weekly report settings', err);
          reject(err);
        },
      });
    });
  }

  updateOrganisation(data: Partial<Organisation>): Promise<Organisation> {
    return new Promise((resolve, reject) => {
      this.http.patch<Organisation>(
        `/api/organisations/${this.currentOrgId}`,
        data
      ).subscribe({
        next: (org) => {
          this.organisation.set(org);
          resolve(org);
        },
        error: (err) => {
          console.error('Failed to update organisation', err);
          reject(err);
        },
      });
    });
  }

  // Helper methods
  getInitials(name: string): string {
    return name
      .split(' ')
      .map(n => n[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  }

  getStatusColor(availability: string): string {
    const colors: Record<string, string> = {
      Available: 'bg-status-available',
      Away: 'bg-status-away',
      BeRightBack: 'bg-status-away',
      Busy: 'bg-status-busy',
      DoNotDisturb: 'bg-status-dnd',
      Offline: 'bg-status-offline',
    };
    return colors[availability] || 'bg-surface-400';
  }

  getStatusLabel(availability: string): string {
    const labels: Record<string, string> = {
      Available: 'Available',
      Away: 'Away',
      BeRightBack: 'Be right back',
      Busy: 'Busy',
      DoNotDisturb: 'Do not disturb',
      Offline: 'Offline',
    };
    return labels[availability] || availability;
  }

  clear(): void {
    this.organisation.set(null);
    this.members.set([]);
    this.loading.set(false);
    this.error.set(null);
    this.currentOrgId = '';
  }
}
