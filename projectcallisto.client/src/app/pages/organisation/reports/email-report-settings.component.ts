import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { OrganisationService } from '../organisation.service';

interface EmailRecipient {
  email: string;
  name: string | null;
}

interface EmailReportSettings {
  id: string | null;
  isEnabled: boolean;
  frequency: 'daily' | 'weekly' | 'monthly';
  timeOfDay: string; // "HH:00:00"
  dayOfWeek: string | null; // "monday", "tuesday", etc. for Weekly
  dayOfMonth: number | null; // 1-28 for Monthly
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
  allSettings = signal<EmailReportSettings[]>([]);

  // Daily report state
  dailySetting = computed(() => this.allSettings().find(s => s.frequency === 'daily'));
  dailyEnabled = signal(false);
  dailyTime = signal(9);
  dailyRecipients = signal<EmailRecipient[]>([]);
  dailySaving = signal(false);
  dailySendingSample = signal(false);
  dailyNewEmail = signal('');
  dailyNewName = signal('');

  // Weekly report state
  weeklySetting = computed(() => this.allSettings().find(s => s.frequency === 'weekly'));
  weeklyEnabled = signal(false);
  weeklyDay = signal(1); // Monday
  weeklyTime = signal(9);
  weeklyRecipients = signal<EmailRecipient[]>([]);
  weeklySaving = signal(false);
  weeklySendingSample = signal(false);
  weeklyNewEmail = signal('');
  weeklyNewName = signal('');

  // Monthly report state
  monthlySetting = computed(() => this.allSettings().find(s => s.frequency === 'monthly'));
  monthlyEnabled = signal(false);
  monthlyDay = signal(1);
  monthlyTime = signal(9);
  monthlyRecipients = signal<EmailRecipient[]>([]);
  monthlySaving = signal(false);
  monthlySendingSample = signal(false);
  monthlyNewEmail = signal('');
  monthlyNewName = signal('');

  ngOnInit(): void {
    this.loadSettings();
  }

  async loadSettings(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org) {
      console.error('No organisation available');
      return;
    }

    this.loading.set(true);
    try {
      // Initialize settings (creates missing ones with defaults)
      const settings = await this.http
        .post<EmailReportSettings[]>(`/api/organisations/${org.id}/email-report-settings/initialize`, {})
        .toPromise();

      this.allSettings.set(settings || []);

      // Load Daily settings
      const daily = settings?.find(s => s.frequency === 'daily');
      if (daily) {
        console.log('Loading daily settings:', daily);
        this.dailyEnabled.set(daily.isEnabled);
        this.dailyTime.set(this.parseTimeOfDay(daily.timeOfDay));
        this.dailyRecipients.set([...daily.recipients]);
      }

      // Load Weekly settings
      const weekly = settings?.find(s => s.frequency === 'weekly');
      if (weekly) {
        console.log('Loading weekly settings:', weekly);
        this.weeklyEnabled.set(weekly.isEnabled);
        this.weeklyDay.set(this.parseDayOfWeekString(weekly.dayOfWeek));
        this.weeklyTime.set(this.parseTimeOfDay(weekly.timeOfDay));
        this.weeklyRecipients.set([...weekly.recipients]);
      }

      // Load Monthly settings
      const monthly = settings?.find(s => s.frequency === 'monthly');
      if (monthly) {
        console.log('Loading monthly settings:', monthly);
        this.monthlyEnabled.set(monthly.isEnabled);
        this.monthlyDay.set(monthly.dayOfMonth ?? 1);
        this.monthlyTime.set(this.parseTimeOfDay(monthly.timeOfDay));
        this.monthlyRecipients.set([...monthly.recipients]);
      }
    } catch (err) {
      console.error('Failed to load email report settings', err);
    } finally {
      this.loading.set(false);
    }
  }

  // Daily report methods
  async saveDailySettings(): Promise<void> {
    const org = this.orgService.organisation();
    const existing = this.dailySetting();

    console.log('saveDailySettings called', { org, existing });

    if (!org) {
      console.error('No organisation found');
      return;
    }

    if (!existing) {
      console.error('No existing daily setting found');
      return;
    }

    if (!existing.id) {
      console.error('Daily setting has no ID', existing);
      return;
    }

    this.dailySaving.set(true);
    try {
      const payload = {
        isEnabled: this.dailyEnabled(),
        frequency: 'daily',
        timeOfDay: `${this.dailyTime().toString().padStart(2, '0')}:00:00`,
        dayOfWeek: null,
        dayOfMonth: null,
        recipients: this.dailyRecipients(),
      };

      console.log('Sending PUT request', payload);

      const updated = await this.http
        .put<EmailReportSettings>(`/api/organisations/${org.id}/email-report-settings/${existing.id}`, payload)
        .toPromise() as EmailReportSettings;

      console.log('Successfully updated', updated);

      // Update local state
      this.allSettings.update(settings => {
        const filtered = settings.filter(s => s.frequency !== 'daily');
        return [...filtered, updated];
      });
    } catch (err) {
      console.error('Failed to save daily settings', err);
    } finally {
      this.dailySaving.set(false);
    }
  }

  addDailyRecipient(): void {
    const email = this.dailyNewEmail().trim();
    if (!email || !this.isValidEmail(email)) return;

    const name = this.dailyNewName().trim() || null;
    if (this.dailyRecipients().some(r => r.email.toLowerCase() === email.toLowerCase())) return;

    this.dailyRecipients.update(recipients => [...recipients, { email, name }]);
    this.dailyNewEmail.set('');
    this.dailyNewName.set('');
  }

  removeDailyRecipient(index: number): void {
    this.dailyRecipients.update(recipients => recipients.filter((_, i) => i !== index));
  }

  // Weekly report methods
  async saveWeeklySettings(): Promise<void> {
    const org = this.orgService.organisation();
    const existing = this.weeklySetting();

    console.log('saveWeeklySettings called', { org, existing });

    if (!org) {
      console.error('No organisation found');
      return;
    }

    if (!existing) {
      console.error('No existing weekly setting found');
      return;
    }

    if (!existing.id) {
      console.error('Weekly setting has no ID', existing);
      return;
    }

    this.weeklySaving.set(true);
    try {
      const payload = {
        isEnabled: this.weeklyEnabled(),
        frequency: 'weekly',
        timeOfDay: `${this.weeklyTime().toString().padStart(2, '0')}:00:00`,
        dayOfWeek: this.getDayOfWeekString(this.weeklyDay()),
        dayOfMonth: null,
        recipients: this.weeklyRecipients(),
      };

      console.log('Sending PUT request for weekly', payload);

      const updated = await this.http
        .put<EmailReportSettings>(`/api/organisations/${org.id}/email-report-settings/${existing.id}`, payload)
        .toPromise() as EmailReportSettings;

      console.log('Successfully updated weekly', updated);

      this.allSettings.update(settings => {
        const filtered = settings.filter(s => s.frequency !== 'weekly');
        return [...filtered, updated];
      });
    } catch (err: any) {
      console.error('Failed to save weekly settings', err);
      console.error('Error response:', err.error);
    } finally {
      this.weeklySaving.set(false);
    }
  }

  addWeeklyRecipient(): void {
    const email = this.weeklyNewEmail().trim();
    if (!email || !this.isValidEmail(email)) return;

    const name = this.weeklyNewName().trim() || null;
    if (this.weeklyRecipients().some(r => r.email.toLowerCase() === email.toLowerCase())) return;

    this.weeklyRecipients.update(recipients => [...recipients, { email, name }]);
    this.weeklyNewEmail.set('');
    this.weeklyNewName.set('');
  }

  removeWeeklyRecipient(index: number): void {
    this.weeklyRecipients.update(recipients => recipients.filter((_, i) => i !== index));
  }

  // Monthly report methods
  async saveMonthlySettings(): Promise<void> {
    const org = this.orgService.organisation();
    const existing = this.monthlySetting();

    console.log('saveMonthlySettings called', { org, existing });

    if (!org) {
      console.error('No organisation found');
      return;
    }

    if (!existing) {
      console.error('No existing monthly setting found');
      return;
    }

    if (!existing.id) {
      console.error('Monthly setting has no ID', existing);
      return;
    }

    this.monthlySaving.set(true);
    try {
      const payload = {
        isEnabled: this.monthlyEnabled(),
        frequency: 'monthly',
        timeOfDay: `${this.monthlyTime().toString().padStart(2, '0')}:00:00`,
        dayOfWeek: null,
        dayOfMonth: this.monthlyDay(),
        recipients: this.monthlyRecipients(),
      };

      console.log('Sending PUT request for monthly', payload);

      const updated = await this.http
        .put<EmailReportSettings>(`/api/organisations/${org.id}/email-report-settings/${existing.id}`, payload)
        .toPromise() as EmailReportSettings;

      console.log('Successfully updated monthly', updated);

      this.allSettings.update(settings => {
        const filtered = settings.filter(s => s.frequency !== 'monthly');
        return [...filtered, updated];
      });
    } catch (err: any) {
      console.error('Failed to save monthly settings', err);
      console.error('Error response:', err.error);
    } finally {
      this.monthlySaving.set(false);
    }
  }

  addMonthlyRecipient(): void {
    const email = this.monthlyNewEmail().trim();
    if (!email || !this.isValidEmail(email)) return;

    const name = this.monthlyNewName().trim() || null;
    if (this.monthlyRecipients().some(r => r.email.toLowerCase() === email.toLowerCase())) return;

    this.monthlyRecipients.update(recipients => [...recipients, { email, name }]);
    this.monthlyNewEmail.set('');
    this.monthlyNewName.set('');
  }

  removeMonthlyRecipient(index: number): void {
    this.monthlyRecipients.update(recipients => recipients.filter((_, i) => i !== index));
  }

  // Send sample email methods
  async sendDailySample(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org) return;

    this.dailySendingSample.set(true);
    try {
      const response = await this.http
        .post<{ message: string }>(`/api/organisations/${org.id}/email-report-settings/send-sample`, {
          frequency: 'Daily'
        })
        .toPromise();

      console.log('Sample email sent:', response?.message);
      alert(response?.message || 'Sample email sent successfully!');
    } catch (err) {
      console.error('Failed to send sample email', err);
      alert('Failed to send sample email. Please try again.');
    } finally {
      this.dailySendingSample.set(false);
    }
  }

  async sendWeeklySample(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org) return;

    this.weeklySendingSample.set(true);
    try {
      const response = await this.http
        .post<{ message: string }>(`/api/organisations/${org.id}/email-report-settings/send-sample`, {
          frequency: 'Weekly'
        })
        .toPromise();

      console.log('Sample email sent:', response?.message);
      alert(response?.message || 'Sample email sent successfully!');
    } catch (err) {
      console.error('Failed to send sample email', err);
      alert('Failed to send sample email. Please try again.');
    } finally {
      this.weeklySendingSample.set(false);
    }
  }

  async sendMonthlySample(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org) return;

    this.monthlySendingSample.set(true);
    try {
      const response = await this.http
        .post<{ message: string }>(`/api/organisations/${org.id}/email-report-settings/send-sample`, {
          frequency: 'Monthly'
        })
        .toPromise();

      console.log('Sample email sent:', response?.message);
      alert(response?.message || 'Sample email sent successfully!');
    } catch (err) {
      console.error('Failed to send sample email', err);
      alert('Failed to send sample email. Please try again.');
    } finally {
      this.monthlySendingSample.set(false);
    }
  }

  // Utility methods
  isValidEmail(email: string): boolean {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
  }

  parseTimeOfDay(timeString: string): number {
    const parts = timeString.split(':');
    return parseInt(parts[0], 10);
  }

  getDayName(day: number): string {
    const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
    return days[day];
  }

  getDayOfWeekString(day: number): string {
    const days = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'];
    return days[day];
  }

  parseDayOfWeekString(dayString: string | null): number {
    const days = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'];
    const index = days.indexOf(dayString?.toLowerCase() || '');
    return index >= 0 ? index : 1; // Default to Monday (1) if not found
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
