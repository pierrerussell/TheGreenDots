import { Component, signal, inject, OnInit } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';

type Step = 'connect' | 'loading' | 'preview' | 'pricing';

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
}

@Component({
  selector: 'app-add-organization',
  standalone: true,
  templateUrl: './add-organization.component.html',
  styleUrl: './add-organization.component.scss',
})
export class AddOrganizationComponent implements OnInit {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);

  currentStep = signal<Step>('connect');
  selectedPlan = signal<'trial' | 'basic'>('trial');
  loadingMessage = signal('Connecting to Microsoft...');
  tenantName = signal('Contoso Ltd');
  teamMembers = signal<TeamMember[]>([]);

  steps = [
    { id: 'connect' as Step, label: 'Connect' },
    { id: 'preview' as Step, label: 'Preview' },
    { id: 'pricing' as Step, label: 'Plan' },
  ];

  private stepOrder: Step[] = ['connect', 'loading', 'preview', 'pricing'];

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

    this.http.get<Organisation>(`/api/organisations/${orgId}`).subscribe({
      next: (org) => {
        this.tenantName.set(org.name);
        this.loadMembers(orgId);
      },
      error: (err) => {
        console.error('Failed to load organisation', err);
        this.currentStep.set('connect');
      },
    });
  }

  private loadMembers(orgId: string): void {
    this.loadingMessage.set('Loading team members...');

    this.http.get<TeamMember[]>(`/api/organisations/${orgId}/members`).subscribe({
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

  continueToPricing(): void {
    this.currentStep.set('pricing');
  }

  selectPlan(plan: 'trial' | 'basic'): void {
    this.selectedPlan.set(plan);
  }

  completeSetup(): void {
    // TODO: Create organization, start subscription
    console.log('Completing setup with plan:', this.selectedPlan());
    this.router.navigate(['/']);
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
    return colors[availability] || 'bg-stone-300';
  }
}
