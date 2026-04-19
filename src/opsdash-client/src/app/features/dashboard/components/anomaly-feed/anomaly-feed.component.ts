import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { Anomaly } from '../../models/dashboard.models';
import { formatMetricTitle, formatMetricValue } from '../../utils/metric-format';
import { formatTimeAgo } from '../../utils/time-ago';

@Component({
  selector: 'app-anomaly-feed',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './anomaly-feed.component.html',
  styleUrl: './anomaly-feed.component.scss',
})
export class AnomalyFeedComponent {
  readonly anomalies = input<Anomaly[]>([]);

  readonly anomalyClick = output<Anomaly>();

  formatTitle = formatMetricTitle;
  timeAgo = formatTimeAgo;

  severityClass(sev: string): string {
    const s = (sev || '').toLowerCase();
    if (s.includes('severe')) {
      return 'sev-severe';
    }

    if (s.includes('critical')) {
      return 'sev-critical';
    }

    return 'sev-warning';
  }

  onRowClick(a: Anomaly): void {
    this.anomalyClick.emit(a);
  }

  formatAnomalyValue(a: Anomaly): string {
    return formatMetricValue(a.metricName, a.metricValue);
  }

  formatZ(z: number): string {
    return z.toLocaleString(undefined, { maximumFractionDigits: 1, minimumFractionDigits: 1 });
  }
}
