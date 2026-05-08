import { Component, signal, inject, OnInit, OnDestroy, effect } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ToastService } from '../../shared/toast.service';

type Step = 'connect' | 'loading' | 'preview' | 'timezone' | 'working-hours' | 'trial-status';

export interface TeamMember {
  id: string;
  displayName: string;
  email: string | null;
  jobTitle: string | null;
  availability: string;
  activity: string | null;
}

interface Organisation {
  id: string;
  name: string;
  tenantId: string;
  timezone: string;
}

interface Subscription {
  id: string;
  status: 'Trial' | 'Active' | 'PastDue' | 'Cancelled';
  paidSeats: number;
  trialEndsAt: string | null;
}

interface WorkingHours {
  id: string | null;
  startTime: string; // "HH:mm:ss"
  endTime: string; // "HH:mm:ss"
  workingDays: string[]; // ["monday", "tuesday", etc.]
}

@Component({
  selector: 'app-add-organization',
  standalone: true,
  templateUrl: './add-organization.component.html',
  styleUrl: './add-organization.component.scss',
})
export class AddOrganizationComponent implements OnInit, OnDestroy {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private toast = inject(ToastService);
  private timeUpdateInterval: any = null;

  currentStep = signal<Step>('connect');
  loadingMessage = signal('Connecting to Microsoft...');
  tenantName = signal('Contoso Ltd');
  teamMembers = signal<TeamMember[]>([]);
  orgId = signal<string | null>(null);
  orgTimezone = signal('UTC');
  selectedTimezone = signal('UTC');
  autoDetectedTimezone = signal('UTC');
  currentTimeInTimezone = signal('');
  subscription = signal<Subscription | null>(null);

  // Working hours with smart defaults (9 AM - 5 PM, Mon-Fri)
  workingHours = signal<WorkingHours>({
    id: null,
    startTime: '09:00:00',
    endTime: '17:00:00',
    workingDays: ['monday', 'tuesday', 'wednesday', 'thursday', 'friday']
  });

  workingDaysOptions = [
    { value: 'monday', label: 'Monday' },
    { value: 'tuesday', label: 'Tuesday' },
    { value: 'wednesday', label: 'Wednesday' },
    { value: 'thursday', label: 'Thursday' },
    { value: 'friday', label: 'Friday' },
    { value: 'saturday', label: 'Saturday' },
    { value: 'sunday', label: 'Sunday' },
  ];

  commonTimezones = [
    { value: 'UTC', label: 'UTC (Coordinated Universal Time)' },
    { value: 'America/New_York', label: 'Eastern Time (US & Canada)' },
    { value: 'America/Chicago', label: 'Central Time (US & Canada)' },
    { value: 'America/Denver', label: 'Mountain Time (US & Canada)' },
    { value: 'America/Los_Angeles', label: 'Pacific Time (US & Canada)' },
    { value: 'Europe/London', label: 'London (GMT/BST)' },
    { value: 'Europe/Paris', label: 'Paris (CET/CEST)' },
    { value: 'Europe/Berlin', label: 'Berlin (CET/CEST)' },
    { value: 'Asia/Tokyo', label: 'Tokyo (JST)' },
    { value: 'Asia/Shanghai', label: 'Shanghai (CST)' },
    { value: 'Asia/Singapore', label: 'Singapore (SGT)' },
    { value: 'Asia/Dubai', label: 'Dubai (GST)' },
    { value: 'Asia/Kolkata', label: 'India (IST)' },
    { value: 'Australia/Sydney', label: 'Sydney (AEDT/AEST)' },
    { value: 'Pacific/Auckland', label: 'Auckland (NZDT/NZST)' },
    { value: 'America/Sao_Paulo', label: 'São Paulo (BRT)' },
    { value: 'America/Mexico_City', label: 'Mexico City (CST)' },
    { value: 'America/Toronto', label: 'Toronto (EST/EDT)' },
    { value: 'Africa/Johannesburg', label: 'Johannesburg (SAST)' },
  ];

  steps = [
    { id: 'connect' as Step, label: 'Connect' },
    { id: 'preview' as Step, label: 'Preview' },
    { id: 'timezone' as Step, label: 'Timezone' },
    { id: 'working-hours' as Step, label: 'Working Hours' },
    { id: 'trial-status' as Step, label: 'Trial Status' },
  ];

  private stepOrder: Step[] = ['connect', 'loading', 'preview', 'timezone', 'working-hours', 'trial-status'];

  constructor() {
    // Update time when on timezone step
    effect(() => {
      if (this.currentStep() === 'timezone') {
        this.updateCurrentTime();
        this.timeUpdateInterval = setInterval(() => this.updateCurrentTime(), 1000);
      } else {
        if (this.timeUpdateInterval) {
          clearInterval(this.timeUpdateInterval);
          this.timeUpdateInterval = null;
        }
      }
    });
  }

  ngOnInit(): void {
    const success = this.route.snapshot.queryParamMap.get('success');
    const orgId = this.route.snapshot.queryParamMap.get('orgId');

    if (success === 'true' && orgId) {
      this.loadOrganisation(orgId);
    }
  }

  private loadOrganisation(orgId: string): void {
    this.currentStep.set('loading');
    this.loadingMessage.set('Loading organisation details...');
    this.orgId.set(orgId);

    this.http.get<Organisation>(`/api/organisations/${orgId}`).subscribe({
      next: (org) => {
        this.tenantName.set(org.name);
        const detectedTimezone = org.timezone || 'UTC';
        this.orgTimezone.set(detectedTimezone);
        this.autoDetectedTimezone.set(detectedTimezone);
        this.selectedTimezone.set(detectedTimezone);
        this.updateCurrentTime();
        this.loadSubscription(orgId);
        this.loadMembers(orgId);
      },
      error: (err) => {
        console.error('Failed to load organisation', err);
        this.currentStep.set('connect');
      },
    });
  }

  private loadSubscription(orgId: string): void {
    this.http.get<Subscription>(`/api/organisations/${orgId}/subscription`).subscribe({
      next: (subscription) => {
        this.subscription.set(subscription);
      },
      error: (err) => {
        console.error('Failed to load subscription', err);
        // Continue anyway - we'll show a generic message
      },
    });
  }

  private loadMembers(orgId: string): void {
    this.loadingMessage.set('Loading team members...');

    // Use preview endpoint (returns ALL members with live presence, regardless of IsAssignedSeat)
    this.http.get<TeamMember[]>(`/api/organisations/${orgId}/members/preview`).subscribe({
      next: (members) => {
        this.teamMembers.set(members);
        this.currentStep.set('preview');
      },
      error: (err) => {
        console.error('Failed to load members', err);
        this.currentStep.set('preview'); // Still show preview, just without members
      },
    });
  }

  isStepComplete(stepId: Step): boolean {
    const currentIndex = this.stepOrder.indexOf(this.currentStep());
    const stepIndex = this.stepOrder.indexOf(stepId);
    return stepIndex < currentIndex;
  }

  getStepClass(stepId: Step): string {
    if (this.currentStep() === stepId || (this.currentStep() === 'loading' && stepId === 'connect')) {
      return 'bg-brand-500 text-white';
    }
    if (this.isStepComplete(stepId)) {
      return 'bg-brand-500 text-white';
    }
    return 'bg-stone-200 text-stone-500';
  }

  goBack(): void {
    const currentIndex = this.stepOrder.indexOf(this.currentStep());
    if (currentIndex > 0) {
      const prevStep = this.stepOrder[currentIndex - 1];
      if (prevStep === 'loading') {
        this.currentStep.set('connect');
      } else {
        this.currentStep.set(prevStep);
      }
    } else {
      this.router.navigate(['/']);
    }
  }

  connectToMicrosoft(): void {
    window.location.href = '/api/auth/microsoft/connect';
  }

  continueToTimezone(): void {
    this.currentStep.set('timezone');
  }

  async continueToWorkingHours(): Promise<void> {
    // Save timezone before moving to working hours
    const orgIdValue = this.orgId();
    if (!orgIdValue) {
      console.error('No organisation ID available');
      return;
    }

    try {
      await this.http
        .put(`/api/organisations/${orgIdValue}/timezone`, {
          timezone: this.selectedTimezone()
        })
        .toPromise();

      this.currentStep.set('working-hours');
    } catch (err) {
      console.error('Failed to save timezone', err);
      this.toast.error('Failed to save timezone. Please try again.');
    }
  }

  continueToTrialStatus(): void {
    this.currentStep.set('trial-status');
  }

  onTimezoneChange(timezone: string): void {
    this.selectedTimezone.set(timezone);
    this.updateCurrentTime();
  }

  updateCurrentTime(): void {
    const tz = this.selectedTimezone();
    try {
      const now = new Date();
      const formatter = new Intl.DateTimeFormat('en-US', {
        timeZone: tz,
        weekday: 'long',
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        timeZoneName: 'short'
      });
      this.currentTimeInTimezone.set(formatter.format(now));
    } catch (err) {
      console.error('Failed to format time for timezone', tz, err);
      this.currentTimeInTimezone.set('Invalid timezone');
    }
  }

  async saveWorkingHoursAndContinue(): Promise<void> {
    const orgIdValue = this.orgId();
    if (!orgIdValue) {
      console.error('No organisation ID available');
      return;
    }

    // Save working hours before continuing
    try {
      const wh = this.workingHours();
      if (wh.workingDays.length === 0) {
        this.toast.error('Please select at least one working day');
        return;
      }

      const payload = {
        startTime: wh.startTime,
        endTime: wh.endTime,
        workingDays: wh.workingDays,
      };

      await this.http
        .put<WorkingHours>(`/api/organisations/${orgIdValue}/working-hours`, payload)
        .toPromise();

      this.continueToTrialStatus();
    } catch (err) {
      console.error('Failed to save working hours', err);
      this.toast.error('Failed to save working hours. Please try again.');
    }
  }

  goToDashboard(): void {
    const orgIdValue = this.orgId();
    if (orgIdValue) {
      this.router.navigate(['/organisation', orgIdValue, 'overview']);
    }
  }

  goToPricing(): void {
    const orgIdValue = this.orgId();
    if (orgIdValue) {
      this.router.navigate(['/organisation', orgIdValue, 'pricing']);
    }
  }

  hasActiveTrial(): boolean {
    const sub = this.subscription();
    if (!sub) return false;

    // Trial is active if:
    // 1. Has paid seats (PaidSeats > 0)
    // 2. Trial hasn't expired (TrialEndsAt is in the future)
    if (sub.paidSeats > 0 && sub.trialEndsAt) {
      const trialEnd = new Date(sub.trialEndsAt);
      return trialEnd > new Date();
    }

    return false;
  }

  getTrialExpiryDate(): string {
    const sub = this.subscription();
    if (!sub || !sub.trialEndsAt) return '';

    const expiryDate = new Date(sub.trialEndsAt);
    return expiryDate.toLocaleDateString('en-US', {
      month: 'long',
      day: 'numeric',
      year: 'numeric'
    });
  }

  toggleWorkingDay(day: string): void {
    const wh = this.workingHours();
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
    return this.workingHours().workingDays.includes(day);
  }

  updateStartTime(time: string): void {
    const wh = this.workingHours();
    this.workingHours.set({
      ...wh,
      startTime: time + ':00', // Convert "HH:mm" to "HH:mm:ss"
    });
  }

  updateEndTime(time: string): void {
    const wh = this.workingHours();
    this.workingHours.set({
      ...wh,
      endTime: time + ':00', // Convert "HH:mm" to "HH:mm:ss"
    });
  }

  getTimeValue(timeString: string): string {
    // Convert "HH:mm:ss" to "HH:mm" for input[type="time"]
    return timeString.substring(0, 5);
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
    // Microsoft Graph availability values: Available, Busy, DoNotDisturb, BeRightBack, Away, Offline
    const colors: Record<string, string> = {
      Available: 'bg-status-available',
      Away: 'bg-status-away',
      BeRightBack: 'bg-status-away',
      Busy: 'bg-status-busy',
      DoNotDisturb: 'bg-status-dnd',
      Offline: 'bg-status-offline',
    };
    return colors[availability] || 'bg-surface-500';
  }

  ngOnDestroy(): void {
    if (this.timeUpdateInterval) {
      clearInterval(this.timeUpdateInterval);
    }
  }
}
