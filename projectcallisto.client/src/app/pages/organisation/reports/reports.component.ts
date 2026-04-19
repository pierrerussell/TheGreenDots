import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { OrganisationService, PresenceTimelineEntry, WeeklyReportSettings } from '../organisation.service';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [FormsModule, DatePipe],
  templateUrl: './reports.component.html',
})
export class ReportsComponent implements OnInit {
  orgService = inject(OrganisationService);

  selectedDate = signal(this.formatDateForInput(new Date()));
  timeline = signal<PresenceTimelineEntry[]>([]);
  loadingTimeline = signal(true);

  // Weekly report settings
  weeklySettings = signal<WeeklyReportSettings>({
    enabled: false,
    recipients: [],
    dayOfWeek: 1, // Monday
    timeUtc: '09:00',
  });
  loadingSettings = signal(true);
  savingSettings = signal(false);
  newRecipient = signal('');

  // Hours to display
  hours = Array.from({ length: 17 }, (_, i) => i + 6);
  daysOfWeek = [
    { value: 0, label: 'Sunday' },
    { value: 1, label: 'Monday' },
    { value: 2, label: 'Tuesday' },
    { value: 3, label: 'Wednesday' },
    { value: 4, label: 'Thursday' },
    { value: 5, label: 'Friday' },
    { value: 6, label: 'Saturday' },
  ];

  ngOnInit(): void {
    this.loadTimeline();
    this.loadWeeklySettings();
  }

  private loadTimeline(): void {
    this.loadingTimeline.set(true);

    // Parse the selected date and calculate UTC timestamps for start/end of day
    const date = new Date(this.selectedDate() + 'T00:00:00');

    const startOfDay = new Date(date);
    startOfDay.setHours(0, 0, 0, 0);

    const endOfDay = new Date(date);
    endOfDay.setHours(23, 59, 59, 999);

    const startTime = startOfDay.toISOString();
    const endTime = endOfDay.toISOString();

    this.orgService.getPresenceTimeline(startTime, endTime)
      .then(data => {
        this.timeline.set(data);
        this.loadingTimeline.set(false);
      })
      .catch(() => {
        this.timeline.set([]);
        this.loadingTimeline.set(false);
      });
  }

  private loadWeeklySettings(): void {
    this.loadingSettings.set(true);

    this.orgService.getWeeklyReportSettings()
      .then(settings => {
        this.weeklySettings.set(settings);
        this.loadingSettings.set(false);
      })
      .catch(() => {
        this.loadingSettings.set(false);
      });
  }

  onDateChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedDate.set(input.value);
    this.loadTimeline();
  }

  goToPreviousDay(): void {
    const date = new Date(this.selectedDate());
    date.setDate(date.getDate() - 1);
    this.selectedDate.set(this.formatDateForInput(date));
    this.loadTimeline();
  }

  goToNextDay(): void {
    const date = new Date(this.selectedDate());
    date.setDate(date.getDate() + 1);
    const today = new Date();
    if (date <= today) {
      this.selectedDate.set(this.formatDateForInput(date));
      this.loadTimeline();
    }
  }

  goToToday(): void {
    this.selectedDate.set(this.formatDateForInput(new Date()));
    this.loadTimeline();
  }

  isToday(): boolean {
    return this.selectedDate() === this.formatDateForInput(new Date());
  }

  private formatDateForInput(date: Date): string {
    return date.toISOString().split('T')[0];
  }

  getDisplayDate(): Date {
    return new Date(this.selectedDate() + 'T00:00:00');
  }

  formatHour(hour: number): string {
    if (hour === 0) return '12am';
    if (hour === 12) return '12pm';
    if (hour < 12) return `${hour}am`;
    return `${hour - 12}pm`;
  }

  getTimelineSegments(entries: PresenceTimelineEntry['entries']): { left: string; width: string; status: string }[] {
    const dayStart = 6;
    const dayEnd = 23;
    const totalMinutes = (dayEnd - dayStart) * 60;

    return entries
      .filter(entry => {
        const start = new Date(entry.startTime);
        const startHour = start.getHours();
        const end = entry.endTime ? new Date(entry.endTime) : new Date();
        const endHour = end.getHours();
        return startHour < dayEnd && endHour >= dayStart;
      })
      .map(entry => {
        const start = new Date(entry.startTime);
        const end = entry.endTime ? new Date(entry.endTime) : new Date();

        const clampedStart = Math.max(0, (start.getHours() - dayStart) * 60 + start.getMinutes());
        const clampedEnd = Math.min(totalMinutes, (end.getHours() - dayStart) * 60 + end.getMinutes());

        const left = (clampedStart / totalMinutes) * 100;
        const width = Math.max(0.5, ((clampedEnd - clampedStart) / totalMinutes) * 100);

        return { left: `${left}%`, width: `${width}%`, status: entry.status };
      });
  }

  getSegmentColor(status: string): string {
    const colors: Record<string, string> = {
      Available: 'bg-status-available',
      Away: 'bg-status-away',
      BeRightBack: 'bg-status-away',
      Busy: 'bg-status-busy',
      DoNotDisturb: 'bg-status-dnd',
      Offline: 'bg-status-offline',
    };
    return colors[status] || 'bg-surface-300';
  }

  // Weekly report settings methods
  toggleWeeklyReport(): void {
    this.weeklySettings.update(s => ({ ...s, enabled: !s.enabled }));
  }

  updateDayOfWeek(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.weeklySettings.update(s => ({ ...s, dayOfWeek: parseInt(select.value, 10) }));
  }

  updateTime(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.weeklySettings.update(s => ({ ...s, timeUtc: input.value }));
  }

  addRecipient(): void {
    const email = this.newRecipient().trim();
    if (email && !this.weeklySettings().recipients.includes(email)) {
      this.weeklySettings.update(s => ({
        ...s,
        recipients: [...s.recipients, email],
      }));
      this.newRecipient.set('');
    }
  }

  removeRecipient(email: string): void {
    this.weeklySettings.update(s => ({
      ...s,
      recipients: s.recipients.filter(r => r !== email),
    }));
  }

  async saveWeeklySettings(): Promise<void> {
    this.savingSettings.set(true);
    try {
      await this.orgService.updateWeeklyReportSettings(this.weeklySettings());
    } catch (err) {
      console.error('Failed to save settings', err);
    } finally {
      this.savingSettings.set(false);
    }
  }

  exportCsv(): void {
    // TODO: Implement CSV export
    console.log('Exporting CSV for', this.selectedDate());
  }
}
