import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
interface Organisation {
  id: string;
  name: string;
  tenantId: string;
  createdAt: string;
}

interface TeamMember {
  id: string;
  displayName: string;
  email: string | null;
  jobTitle: string | null;
  availability: string;
  activity: string | null;
  lastStatusChange?: string;
}

interface PresenceChange {
  id: string;
  memberId: string;
  memberName: string;
  previousStatus: string;
  newStatus: string;
  timestamp: string;
}

@Component({
  selector: 'app-organisation',
  standalone: true,
  templateUrl: './organisation.component.html',
  styleUrl: './organisation.component.scss',
})
export class OrganisationComponent implements OnInit {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);

  organisation = signal<Organisation | null>(null);
  members = signal<TeamMember[]>([]);
  presenceChanges = signal<PresenceChange[]>([]);
  loading = signal(true);
  activeTab = signal<'team' | 'activity'>('team');

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

  ngOnInit(): void {
    const orgId = this.route.snapshot.paramMap.get('id');
    if (orgId) {
      this.loadOrganisation(orgId);
    }
  }

  private loadOrganisation(orgId: string): void {
    this.http.get<Organisation>(`/api/organisations/${orgId}`).subscribe({
      next: (org) => {
        this.organisation.set(org);
        this.loadMembers(orgId);
        this.loadPresenceHistory(orgId);
      },
      error: () => {
        this.loading.set(false);
        this.router.navigate(['/']);
      },
    });
  }

  private loadMembers(orgId: string): void {
    this.http.get<TeamMember[]>(`/api/organisations/${orgId}/members`).subscribe({
      next: (members) => {
        this.members.set(members);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  private loadPresenceHistory(orgId: string): void {
    this.http.get<PresenceChange[]>(`/api/organisations/${orgId}/presence-history`).subscribe({
      next: (changes) => {
        this.presenceChanges.set(changes);
      },
      error: () => {
        // Silently fail - activity feed is secondary
      },
    });
  }

  goBack(): void {
    this.router.navigate(['/']);
  }

  setActiveTab(tab: 'team' | 'activity'): void {
    this.activeTab.set(tab);
  }

  refreshData(): void {
    const orgId = this.route.snapshot.paramMap.get('id');
    if (orgId) {
      this.loadMembers(orgId);
      this.loadPresenceHistory(orgId);
    }
  }

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

  formatTimeAgo(timestamp: string): string {
    const now = new Date();
    const then = new Date(timestamp);
    const diffMs = now.getTime() - then.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays === 1) return 'Yesterday';
    return `${diffDays}d ago`;
  }
}
