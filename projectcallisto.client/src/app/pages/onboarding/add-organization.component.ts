import { Component, signal, inject } from '@angular/core';
import { Router } from '@angular/router';

type Step = 'connect' | 'loading' | 'preview' | 'pricing';

export interface TeamMember {
  id: string;
  displayName: string;
  email: string;
  status: 'available' | 'away' | 'busy' | 'dnd' | 'offline';
}

@Component({
  selector: 'app-add-organization',
  standalone: true,
  templateUrl: './add-organization.component.html',
  styleUrl: './add-organization.component.scss',
})
export class AddOrganizationComponent {
  private router = inject(Router);

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
    this.currentStep.set('loading');
    this.loadingMessage.set('Connecting to Microsoft...');

    // Simulate OAuth flow
    setTimeout(() => {
      this.loadingMessage.set('Fetching team members...');
    }, 1000);

    setTimeout(() => {
      // Simulate loaded data
      this.teamMembers.set([
        { id: '1', displayName: 'Alice Johnson', email: 'alice@contoso.com', status: 'available' },
        { id: '2', displayName: 'Bob Smith', email: 'bob@contoso.com', status: 'busy' },
        { id: '3', displayName: 'Carol Williams', email: 'carol@contoso.com', status: 'away' },
        { id: '4', displayName: 'David Brown', email: 'david@contoso.com', status: 'offline' },
        { id: '5', displayName: 'Eve Davis', email: 'eve@contoso.com', status: 'dnd' },
        { id: '6', displayName: 'Frank Miller', email: 'frank@contoso.com', status: 'available' },
      ]);
      this.currentStep.set('preview');
    }, 2000);
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

  getStatusColor(status: string): string {
    const colors: Record<string, string> = {
      available: 'bg-status-available',
      away: 'bg-status-away',
      busy: 'bg-status-busy',
      dnd: 'bg-status-dnd',
      offline: 'bg-status-offline',
    };
    return colors[status] || 'bg-stone-300';
  }
}
