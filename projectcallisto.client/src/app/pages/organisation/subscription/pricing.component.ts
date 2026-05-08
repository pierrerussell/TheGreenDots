import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { OrganisationService } from '../organisation.service';

interface CurrentSubscription {
  status: string;
  paidSeats: number;
  currentPeriodEnd?: string;
  billingInterval?: string;
  pricePerSeat?: number;
  trialEndsAt?: string;
  stripeSubscriptionId?: string;
  cancelAtPeriodEnd: boolean;
  cancelAt?: string;
}

@Component({
  selector: 'app-pricing',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './pricing.component.html',
  styleUrls: ['./pricing.component.scss']
})
export class PricingComponent implements OnInit {
  private http = inject(HttpClient);
  private orgService = inject(OrganisationService);

  seatCount = signal(10);
  billingInterval = signal<'Monthly' | 'Annual'>('Monthly');
  loading = signal(false);
  currentSubscription = signal<CurrentSubscription | null>(null);

  ngOnInit(): void {
    this.loadCurrentSubscription();
  }

  private loadCurrentSubscription(): void {
    const org = this.orgService.organisation();
    if (!org) return;

    // Fetch subscription details from API (includes Stripe data for active subscriptions)
    this.http.get<CurrentSubscription>(`/api/billing/subscription/${org.id}`)
      .subscribe({
        next: (subscription) => {
          this.currentSubscription.set(subscription);

          // Set initial seat count
          // For trial, default to 10 seats (not the 999 trial seats)
          if (subscription.status === 'Trial') {
            this.seatCount.set(10);
          } else {
            this.seatCount.set(subscription.paidSeats);
          }
        },
        error: (error) => {
          console.error('Failed to load subscription:', error);
          // Fall back to local data if API fails
          if (org.subscription) {
            this.currentSubscription.set({
              status: org.subscription.status,
              paidSeats: org.subscription.paidSeats,
              cancelAtPeriodEnd: false
            });
            this.seatCount.set(org.subscription.status === 'Trial' ? 10 : org.subscription.paidSeats);
          }
        }
      });
  }

  formatDate(dateStr: string): string {
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  // Change detection
  hasChanges = computed(() => {
    const current = this.currentSubscription();
    if (!current) return true; // Always allow if no subscription
    if (current.status === 'Trial') return true; // Always allow for trial (they need to subscribe)

    // Check if seat count changed
    const seatCountChanged = this.seatCount() !== current.paidSeats;

    // Check if billing interval changed (only for active subscriptions)
    const billingIntervalChanged = current.status === 'Active'
      && current.billingInterval
      && this.billingInterval() !== current.billingInterval;

    return seatCountChanged || billingIntervalChanged;
  });

  isOnTrial = computed(() => {
    const current = this.currentSubscription();
    return current?.status === 'Trial';
  });

  isBillingIntervalChange = computed(() => {
    const current = this.currentSubscription();
    if (!current || current.status !== 'Active' || !current.billingInterval) return false;
    return this.billingInterval() !== current.billingInterval;
  });

  isUpgrade = computed(() => {
    const current = this.currentSubscription();
    if (!current) return true;
    if (current.status === 'Trial') return false; // No concept of upgrade for trial
    return this.seatCount() > current.paidSeats;
  });

  // Tier calculation
  tierName = computed(() => {
    const seats = this.seatCount();
    if (seats >= 50) return 'Enterprise';
    if (seats >= 25) return 'Business';
    return 'Standard';
  });

  discount = computed(() => {
    const seats = this.seatCount();
    if (seats >= 50) return 20;
    if (seats >= 25) return 10;
    return 0;
  });

  // Price calculation
  // TODO: These should eventually come from Stripe API via /api/billing/prices endpoint
  pricePerSeat = computed(() => {
    const seats = this.seatCount();
    const isAnnual = this.billingInterval() === 'Annual';

    let monthlyPrice: number;
    if (seats >= 50) {
      monthlyPrice = 1.60;
    } else if (seats >= 25) {
      monthlyPrice = 1.80;
    } else {
      monthlyPrice = 2.00;
    }

    // Annual = monthly * 10 (2 months free)
    return isAnnual ? (monthlyPrice * 10).toFixed(2) : monthlyPrice.toFixed(2);
  });

  subtotal = computed(() => {
    const pricePerSeat = parseFloat(this.pricePerSeat());
    const seats = this.seatCount();
    return (pricePerSeat * seats).toFixed(2);
  });

  totalPrice = computed(() => {
    return this.subtotal();
  });

  annualSavings = computed(() => {
    if (this.billingInterval() === 'Monthly') return '0.00';
    const seats = this.seatCount();
    let monthlyPrice: number;
    if (seats >= 50) {
      monthlyPrice = 1.60;
    } else if (seats >= 25) {
      monthlyPrice = 1.80;
    } else {
      monthlyPrice = 2.00;
    }
    // 2 months savings
    return (monthlyPrice * seats * 2).toFixed(2);
  });

  // Calculate current subscription price for comparison
  currentPricePerSeat = computed(() => {
    const sub = this.currentSubscription();
    if (!sub || !sub.pricePerSeat) return null;
    return sub.pricePerSeat;
  });

  priceDifference = computed(() => {
    const currentTotal = this.currentPricePerSeat();
    if (currentTotal === null) return '0.00';

    const current = this.currentSubscription();
    if (!current) return '0.00';

    // Calculate current total
    const currentPrice = currentTotal * current.paidSeats;

    // Calculate new total
    const newPrice = parseFloat(this.totalPrice());

    const diff = Math.abs(newPrice - currentPrice);
    return diff.toFixed(2);
  });

  subscribe(): void {
    const org = this.orgService.organisation();
    if (!org) {
      alert('Please select an organization first');
      return;
    }

    this.loading.set(true);

    const checkoutRequest = {
      organisationId: org.id,
      seatCount: this.seatCount(),
      billingInterval: this.billingInterval()
    };

    this.http.post<{ success: boolean; sessionId?: string; sessionUrl?: string; message?: string }>('/api/billing/checkout', checkoutRequest)
      .subscribe({
        next: (result) => {
          if (result.sessionUrl) {
            // New subscription or billing interval change - redirect to checkout
            window.location.href = result.sessionUrl;
          } else {
            // Instant update (upgrade/downgrade) - show success and reload
            this.loading.set(false);
            alert(result.message || 'Subscription updated successfully!');
            // Reload subscription data
            this.loadCurrentSubscription();
          }
        },
        error: (error) => {
          console.error('Failed to create checkout session:', error);
          alert('Failed to start checkout. Please try again.');
          this.loading.set(false);
        }
      });
  }

  cancelSubscription(): void {
    const org = this.orgService.organisation();
    if (!org) {
      return;
    }

    const sub = this.currentSubscription();
    if (!sub || sub.status !== 'Active') {
      return;
    }

    if (!confirm('Are you sure you want to cancel your subscription? You will retain access until the end of your current billing period.')) {
      return;
    }

    this.loading.set(true);

    this.http.post<{ message: string }>(`/api/billing/subscription/${org.id}/cancel`, {})
      .subscribe({
        next: (result) => {
          this.loading.set(false);
          alert(result.message);
          // Reload subscription data to show cancellation status
          this.loadCurrentSubscription();
        },
        error: (error) => {
          console.error('Failed to cancel subscription:', error);
          alert('Failed to cancel subscription. Please try again.');
          this.loading.set(false);
        }
      });
  }

  uncancelSubscription(): void {
    const org = this.orgService.organisation();
    if (!org) {
      return;
    }

    const sub = this.currentSubscription();
    if (!sub || sub.status !== 'Active') {
      return;
    }

    this.loading.set(true);

    this.http.post<{ message: string }>(`/api/billing/subscription/${org.id}/uncancel`, {})
      .subscribe({
        next: (result) => {
          this.loading.set(false);
          alert(result.message);
          // Reload subscription data to show reactivated status
          this.loadCurrentSubscription();
        },
        error: (error) => {
          console.error('Failed to reactivate subscription:', error);
          alert('Failed to reactivate subscription. Please try again.');
          this.loading.set(false);
        }
      });
  }
}
