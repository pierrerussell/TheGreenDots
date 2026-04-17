import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { OrganisationService } from '../organisation.service';

interface Subscription {
  id: string;
  plan: 'trial' | 'basic' | 'pro';
  status: 'active' | 'trialing' | 'canceled' | 'past_due';
  currentPeriodStart: string;
  currentPeriodEnd: string;
  trialEndsAt: string | null;
  cancelAtPeriodEnd: boolean;
}

@Component({
  selector: 'app-subscription',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './subscription.component.html',
})
export class SubscriptionComponent implements OnInit {
  private http = inject(HttpClient);
  orgService = inject(OrganisationService);

  subscription = signal<Subscription | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    this.loadSubscription();
  }

  private loadSubscription(): void {
    const org = this.orgService.organisation();
    if (!org) return;

    this.http.get<Subscription>(`/api/organisations/${org.id}/subscription`).subscribe({
      next: (sub) => {
        this.subscription.set(sub);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  getPlanName(plan: string): string {
    const names: Record<string, string> = {
      trial: 'Pro Trial',
      basic: 'Basic',
      pro: 'Pro',
    };
    return names[plan] || plan;
  }

  getPlanPrice(plan: string): string {
    const prices: Record<string, string> = {
      trial: 'Free (14 days)',
      basic: '$5/month',
      pro: '$12/month',
    };
    return prices[plan] || '';
  }

  getStatusBadgeClass(status: string): string {
    const classes: Record<string, string> = {
      active: 'bg-green-50 text-green-700',
      trialing: 'bg-blue-50 text-blue-700',
      canceled: 'bg-surface-100 text-surface-600',
      past_due: 'bg-red-50 text-red-700',
    };
    return classes[status] || 'bg-surface-100 text-surface-600';
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      active: 'Active',
      trialing: 'Trial',
      canceled: 'Canceled',
      past_due: 'Past Due',
    };
    return labels[status] || status;
  }

  getDaysRemaining(dateStr: string): number {
    const end = new Date(dateStr);
    const now = new Date();
    const diff = end.getTime() - now.getTime();
    return Math.max(0, Math.ceil(diff / (1000 * 60 * 60 * 24)));
  }

  manageBilling(): void {
    // TODO: Redirect to Stripe customer portal
    console.log('Managing billing...');
  }

  upgradePlan(): void {
    // TODO: Show upgrade modal or redirect to upgrade page
    console.log('Upgrading plan...');
  }
}
