import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { OrganisationService } from '../organisation.service';

interface EmailRecipient {
  email: string;
  name: string | null;
}

interface EmailReportSettings {
  id: string;
  organisationId: string;
  isEnabled: boolean;
  frequency: 'Daily' | 'Weekly' | 'Monthly';
  timeOfDay: string; // "HH:00:00"
  dayOfWeek: number | null; // 0-6 for Weekly
  dayOfMonth: number | null; // 1-31 for Monthly
  recipients: EmailRecipient[];
  lastSentAt: string | null;
}

@Component({
  selector: 'app-email-report-settings',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './email-report-settings.component.html',
})
export class EmailReportSettingsComponent implements OnInit {
  private http = inject(HttpClient);
  orgService = inject(OrganisationService);

  // State
  loading = signal(true);
  saving = signal(false);
  settings = signal<EmailReportSettings | null>(null);

  // Form state
  isEnabled = signal(false);
  frequency = signal<'Daily' | 'Weekly' | 'Monthly'>('Weekly');
  timeOfDay = signal(9); // Hour 0-23
  dayOfWeek = signal(1); // Monday = 1
  dayOfMonth = signal(1); // 1-31
  recipients = signal<EmailRecipient[]>([]);

  // New recipient form
  newRecipientEmail = signal('');
  newRecipientName = signal('');
  addingRecipient = signal(false);

  // Computed
  hasChanges = computed(() => {
    const current = this.settings();
    if (!current) return false;

    return (
      this.isEnabled() !== current.isEnabled ||
      this.frequency() !== current.frequency ||
      this.timeOfDay() !== this.parseTimeOfDay(current.timeOfDay) ||
      (this.frequency() === 'Weekly' && this.dayOfWeek() !== (current.dayOfWeek ?? 1)) ||
      (this.frequency() === 'Monthly' && this.dayOfMonth() !== (current.dayOfMonth ?? 1)) ||
      JSON.stringify(this.recipients()) !== JSON.stringify(current.recipients)
    );
  });

  canSave = computed(() => {
    return (
      this.hasChanges() &&
      !this.saving() &&
      (this.frequency() !== 'Weekly' || this.dayOfWeek() !== null) &&
      (this.frequency() !== 'Monthly' || this.dayOfMonth() !== null)
    );
  });

  ngOnInit(): void {
    this.loadSettings();
  }

  async loadSettings(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org) return;

    this.loading.set(true);
    try {
      const settings = await this.http
        .get<EmailReportSettings>(`/api/organisations/${org.id}/email-report-settings`)
        .toPromise();

      if (settings) {
        this.settings.set(settings);
        this.isEnabled.set(settings.isEnabled);
        this.frequency.set(settings.frequency);
        this.timeOfDay.set(this.parseTimeOfDay(settings.timeOfDay));
        this.dayOfWeek.set(settings.dayOfWeek ?? 1);
        this.dayOfMonth.set(settings.dayOfMonth ?? 1);
        this.recipients.set([...settings.recipients]);
      }
    } catch (err) {
      console.error('Failed to load email report settings', err);
    } finally {
      this.loading.set(false);
    }
  }

  async saveSettings(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org || !this.canSave()) return;

    this.saving.set(true);
    try {
      const payload = {
        isEnabled: this.isEnabled(),
        frequency: this.frequency(),
        timeOfDay: `${this.timeOfDay().toString().padStart(2, '0')}:00:00`,
        dayOfWeek: this.frequency() === 'Weekly' ? this.dayOfWeek() : null,
        dayOfMonth: this.frequency() === 'Monthly' ? this.dayOfMonth() : null,
        recipients: this.recipients(),
      };

      const updated = await this.http
        .put<EmailReportSettings>(`/api/organisations/${org.id}/email-report-settings`, payload)
        .toPromise();

      if (updated) {
        this.settings.set(updated);
      }
    } catch (err) {
      console.error('Failed to save email report settings', err);
    } finally {
      this.saving.set(false);
    }
  }

  addRecipient(): void {
    const email = this.newRecipientEmail().trim();
    if (!email || !this.isValidEmail(email)) return;

    const name = this.newRecipientName().trim() || null;

    // Check for duplicates
    if (this.recipients().some((r) => r.email.toLowerCase() === email.toLowerCase())) {
      return;
    }

    this.recipients.update((recipients) => [...recipients, { email, name }]);
    this.newRecipientEmail.set('');
    this.newRecipientName.set('');
  }

  removeRecipient(index: number): void {
    this.recipients.update((recipients) => recipients.filter((_, i) => i !== index));
  }

  isValidEmail(email: string): boolean {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
  }

  parseTimeOfDay(timeString: string): number {
    const parts = timeString.split(':');
    return parseInt(parts[0], 10);
  }

  discardChanges(): void {
    const current = this.settings();
    if (!current) return;

    this.isEnabled.set(current.isEnabled);
    this.frequency.set(current.frequency);
    this.timeOfDay.set(this.parseTimeOfDay(current.timeOfDay));
    this.dayOfWeek.set(current.dayOfWeek ?? 1);
    this.dayOfMonth.set(current.dayOfMonth ?? 1);
    this.recipients.set([...current.recipients]);
  }

  getDayName(day: number): string {
    const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
    return days[day];
  }

  formatLastSent(dateString: string | null): string {
    if (!dateString) return 'Never';
    const date = new Date(dateString);
    return date.toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  }
}
