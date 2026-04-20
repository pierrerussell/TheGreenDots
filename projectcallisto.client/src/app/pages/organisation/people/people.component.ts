import { Component, inject, signal, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { OrganisationService } from '../organisation.service';

interface RosterMember {
  id: string;
  displayName: string;
  email: string | null;
  jobTitle: string | null;
  isAssignedSeat: boolean;
}

@Component({
  selector: 'app-people',
  standalone: true,
  templateUrl: './people.component.html',
})
export class PeopleComponent implements OnInit {
  private http = inject(HttpClient);
  orgService = inject(OrganisationService);

  allMembers = signal<RosterMember[]>([]);
  loading = signal(true);
  isAdmin = signal(false);

  syncing = signal(false);
  syncSuccess = signal(false);
  syncError = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.loadAllMembers();
    await this.checkAdminStatus();
  }

  async loadAllMembers(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org) return;

    this.loading.set(true);
    try {
      const members = await this.http.get<RosterMember[]>(
        `/api/organisations/${org.id}/all-members`
      ).toPromise();
      this.allMembers.set(members || []);
    } catch (err) {
      console.error('Failed to load all members', err);
    } finally {
      this.loading.set(false);
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

  async checkAdminStatus(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org) return;

    try {
      const access = await this.orgService.checkAccess(org.id);
      this.isAdmin.set(access.role === 'admin');
    } catch (err) {
      console.error('Failed to check admin status', err);
    }
  }

  async syncFromMicrosoft(): Promise<void> {
    this.syncing.set(true);
    this.syncSuccess.set(false);
    this.syncError.set(null);

    try {
      await this.orgService.syncMembers();
      // Reload all members after sync
      await this.loadAllMembers();
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
