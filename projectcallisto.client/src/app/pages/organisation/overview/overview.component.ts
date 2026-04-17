import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { OrganisationService, PresenceTimelineEntry, TeamMember } from '../organisation.service';

interface MemberWithTimeline extends TeamMember {
  timelineSegments: TimelineSegment[];
}

interface TimelineSegment {
  left: number;
  width: number;
  status: string;
  startTime: string;
  endTime: string;
  durationMinutes: number;
}

@Component({
  selector: 'app-overview',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './overview.component.html',
})
export class OverviewComponent implements OnInit {
  orgService = inject(OrganisationService);

  membersWithTimeline = signal<MemberWithTimeline[]>([]);
  loadingTimeline = signal(true);
  today = new Date();

  // Tooltip state
  hoveredSegment = signal<{ segment: TimelineSegment; x: number; y: number } | null>(null);

  ngOnInit(): void {
    this.loadData();
  }

  private async loadData(): Promise<void> {
    this.loadingTimeline.set(true);

    const dateStr = this.formatDate(this.today);

    try {
      const timeline = await this.orgService.getPresenceTimeline(dateStr);
      this.mergeTimelineWithMembers(timeline);
    } catch {
      // If timeline API fails, show members without timeline data
      // Empty segments = "No data" displayed honestly
      this.membersWithTimeline.set(
        this.orgService.members().map(m => ({ ...m, timelineSegments: [] }))
      );
    }

    this.loadingTimeline.set(false);
  }

  private mergeTimelineWithMembers(timeline: PresenceTimelineEntry[]): void {
    const timelineMap = new Map<string, PresenceTimelineEntry>();
    timeline.forEach(t => timelineMap.set(t.memberId, t));

    const merged = this.orgService.members().map(member => {
      const memberTimeline = timelineMap.get(member.id);
      const segments = memberTimeline
        ? this.calculateSegments(memberTimeline.entries)
        : [];

      return { ...member, timelineSegments: segments };
    });

    this.membersWithTimeline.set(merged);
  }

  private calculateSegments(entries: PresenceTimelineEntry['entries']): TimelineSegment[] {
    const totalMinutes = 24 * 60; // Full day

    return entries.map(entry => {
      const start = new Date(entry.startTime);
      const end = entry.endTime ? new Date(entry.endTime) : new Date();

      const startMinutes = start.getHours() * 60 + start.getMinutes();
      const endMinutes = end.getHours() * 60 + end.getMinutes();

      // Handle segments that might span midnight
      const clampedStart = Math.max(0, startMinutes);
      const clampedEnd = Math.min(totalMinutes, endMinutes > startMinutes ? endMinutes : totalMinutes);

      const left = (clampedStart / totalMinutes) * 100;
      const width = Math.max(0.3, ((clampedEnd - clampedStart) / totalMinutes) * 100);

      return {
        left,
        width,
        status: entry.status,
        startTime: entry.startTime,
        endTime: entry.endTime || new Date().toISOString(),
        durationMinutes: entry.durationMinutes,
      };
    });
  }

  refreshData(): void {
    this.orgService.loadMembers();
    this.loadData();
  }

  private formatDate(date: Date): string {
    // Use local date, not UTC - important for correct day near midnight
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  getSegmentColor(status: string): string {
    // Return actual color values for inline styles (more reliable than Tailwind classes)
    const colors: Record<string, string> = {
      Available: 'oklch(0.60 0.16 145)',
      Away: 'oklch(0.72 0.14 85)',
      BeRightBack: 'oklch(0.72 0.14 85)',
      Busy: 'oklch(0.58 0.20 25)',
      DoNotDisturb: 'oklch(0.52 0.22 25)',
      Offline: 'oklch(0.55 0.02 260)',
    };
    return colors[status] || 'oklch(0.80 0.01 260)';
  }

  formatDuration(minutes: number): string {
    if (minutes < 60) {
      return `${minutes}m`;
    }
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
  }

  formatTime(isoString: string): string {
    const date = new Date(isoString);
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  showTooltip(event: MouseEvent, segment: TimelineSegment): void {
    const rect = (event.target as HTMLElement).getBoundingClientRect();
    this.hoveredSegment.set({
      segment,
      x: rect.left + rect.width / 2,
      y: rect.top,
    });
  }

  hideTooltip(): void {
    this.hoveredSegment.set(null);
  }

  getCurrentTimePosition(): number {
    const now = new Date();
    const minutes = now.getHours() * 60 + now.getMinutes();
    return (minutes / (24 * 60)) * 100;
  }
}
