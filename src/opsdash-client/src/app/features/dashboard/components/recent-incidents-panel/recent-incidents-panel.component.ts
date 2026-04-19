import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import type { IncidentRow } from '../../models/dashboard.models';
import { formatTimeAgo } from '../../utils/time-ago';

@Component({
  selector: 'app-recent-incidents-panel',
  standalone: true,
  imports: [RouterLink, MatIconModule],
  templateUrl: './recent-incidents-panel.component.html',
  styleUrl: './recent-incidents-panel.component.scss',
})
export class RecentIncidentsPanelComponent {
  readonly incidents = input<IncidentRow[]>([]);

  timeAgo = formatTimeAgo;

  severityIcon(sev: string): string {
    const s = (sev || '').toLowerCase();
    if (s.includes('severe')) {
      return 'priority_high';
    }

    if (s.includes('critical')) {
      return 'warning';
    }

    return 'info';
  }

  severityTone(sev: string): string {
    const s = (sev || '').toLowerCase();
    if (s.includes('severe')) {
      return 'sev-severe';
    }

    if (s.includes('critical')) {
      return 'sev-critical';
    }

    return 'sev-warning';
  }

  statusClass(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'open') {
      return 'st-open';
    }

    if (s === 'acknowledged') {
      return 'st-ack';
    }

    if (s === 'investigating') {
      return 'st-inv';
    }

    if (s === 'resolved') {
      return 'st-res';
    }

    return 'st-default';
  }

  truncate(title: string, max = 48): string {
    if (!title || title.length <= max) {
      return title;
    }

    return `${title.slice(0, max - 1)}…`;
  }
}
