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

    // TODO: Replace with actual API call to GET /api/billing/subscription/:orgId
    // This should return: { status, paidSeats, currentPeriodEnd, billingInterval, pricePerSeat }
    // For now, using local data from organisation
    if (org.subscription) {
      this.currentSubscription.set({
        status: org.subscription.status,
        paidSeats: org.subscription.paidSeats,
        // TODO: These should come from Stripe API
        currentPeriodEnd: undefined,
        billingInterval: undefined,
        pricePerSeat: undefined
      });

      // Set initial seat count to current subscription
      this.seatCount.set(org.subscription.paidSeats);
    }
  }

  formatDate(dateStr: string): string {
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  // Change detection
  hasChanges = computed(() => {
    const current = this.currentSubscription();
    if (!current) return true; // Always allow if no subscription
    return this.seatCount() !== current.paidSeats;
  });

  isUpgrade = computed(() => {
    const current = this.currentSubscription();
    if (!current) return true;
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

    this.http.post<{ sessionId: string; sessionUrl: string }>('/api/billing/checkout', checkoutRequest)
      .subscribe({
        next: (result) => {
          window.location.href = result.sessionUrl;
        },
        error: (error) => {
          console.error('Failed to create checkout session:', error);
          alert('Failed to start checkout. Please try again.');
          this.loading.set(false);
        }
      });
  }
}
