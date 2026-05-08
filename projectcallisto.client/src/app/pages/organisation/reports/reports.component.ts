import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { OrganisationService, PresenceTimelineEntry } from '../organisation.service';

interface WorkingHours {
  id: string | null;
  startTime: string; // "HH:mm:ss" format (TimeOnly from API)
  endTime: string; // "HH:mm:ss" format
  workingDays: string[]; // ["monday", "tuesday", "wednesday", etc.]
}

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [FormsModule, DatePipe],
  templateUrl: './reports.component.html',
})
export class ReportsComponent implements OnInit {
  orgService = inject(OrganisationService);
  http = inject(HttpClient);

  selectedDate = signal(this.formatDateForInput(new Date()));
  timeline = signal<PresenceTimelineEntry[]>([]);
  loadingTimeline = signal(true);
  workingHours = signal<WorkingHours | null>(null);

  // Hours to display - full 24-hour view
  hours = Array.from({ length: 24 }, (_, i) => i);

  ngOnInit(): void {
    this.loadTimeline();
    this.loadWorkingHours();
  }

  private async loadWorkingHours(): Promise<void> {
    const org = this.orgService.organisation();
    if (!org) return;

    try {
      const wh = await this.http
        .get<WorkingHours>(`/api/organisations/${org.id}/working-hours`)
        .toPromise();
      this.workingHours.set(wh || null);
    } catch (err) {
      console.log('No working hours configured');
      this.workingHours.set(null);
    }
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
    const dayStart = 0; // Full 24-hour view
    const dayEnd = 24;
    const totalMinutes = (dayEnd - dayStart) * 60;

    return entries.map(entry => {
      const start = new Date(entry.startTime);
      const end = entry.endTime ? new Date(entry.endTime) : new Date();

      const clampedStart = Math.max(0, (start.getHours() - dayStart) * 60 + start.getMinutes());
      const clampedEnd = Math.min(totalMinutes, (end.getHours() - dayStart) * 60 + end.getMinutes());

      const left = (clampedStart / totalMinutes) * 100;
      // Add 0.1% overlap to prevent 1px gaps from rounding errors
      const width = Math.max(0.5, ((clampedEnd - clampedStart) / totalMinutes) * 100 + 0.1);

      return { left: `${left}%`, width: `${width}%`, status: entry.status };
    });
  }

  getWorkingHoursOverlay(): { left: string; width: string } | null {
    const wh = this.workingHours();
    if (!wh) return null;

    // Parse "HH:mm:ss" format
    const parseTime = (timeStr: string): number => {
      const [hours, minutes] = timeStr.split(':').map(Number);
      return hours * 60 + minutes;
    };

    const startMinutes = parseTime(wh.startTime);
    const endMinutes = parseTime(wh.endTime);
    const totalMinutes = 24 * 60;

    const left = (startMinutes / totalMinutes) * 100;
    const width = ((endMinutes - startMinutes) / totalMinutes) * 100;

    return { left: `${left}%`, width: `${width}%` };
  }

  getNonWorkingHoursOverlays(): {
    beforeWork: { left: string; width: string } | null;
    afterWork: { left: string; width: string } | null;
  } | null {
    const wh = this.workingHours();
    if (!wh) return null;

    // Parse "HH:mm:ss" format
    const parseTime = (timeStr: string): number => {
      const [hours, minutes] = timeStr.split(':').map(Number);
      return hours * 60 + minutes;
    };

    const startMinutes = parseTime(wh.startTime);
    const endMinutes = parseTime(wh.endTime);
    const totalMinutes = 24 * 60;

    const beforeWorkWidth = (startMinutes / totalMinutes) * 100;
    const afterWorkLeft = (endMinutes / totalMinutes) * 100;
    const afterWorkWidth = ((totalMinutes - endMinutes) / totalMinutes) * 100;

    return {
      beforeWork: beforeWorkWidth > 0 ? { left: '0%', width: `${beforeWorkWidth}%` } : null,
      afterWork: afterWorkWidth > 0 ? { left: `${afterWorkLeft}%`, width: `${afterWorkWidth}%` } : null,
    };
  }

  isWorkingDay(): boolean {
    const wh = this.workingHours();
    if (!wh) return false;

    const date = new Date(this.selectedDate() + 'T00:00:00');
    const jsDay = date.getDay(); // 0 = Sunday, 1 = Monday, 2 = Tuesday, etc.

    // Map JavaScript day number to day name
    const dayNames = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'];
    const dayName = dayNames[jsDay];

    // Check if this day is in the working days array
    return wh.workingDays.includes(dayName);
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
}
