import { DecimalPipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import type { MetricSummary } from '../../models/dashboard.models';
import { formatMetricTitle, formatMetricValue, isPositiveUpMetric } from '../../utils/metric-format';

@Component({
  selector: 'app-kpi-summary',
  standalone: true,
  imports: [MatCardModule, MatIconModule, DecimalPipe],
  templateUrl: './kpi-summary.component.html',
  styleUrl: './kpi-summary.component.scss',
})
export class KpiSummaryComponent {
  readonly summaries = input<MetricSummary[]>([]);
  readonly selectedCategory = input<string | null>(null);
  readonly selectedMetricName = input<string | null>(null);

  readonly cardSelected = output<string>();

  readonly filteredSummaries = computed(() => {
    const cat = this.selectedCategory();
    const list = this.summaries();
    if (!cat) {
      return list;
    }

    return list.filter((s) => s.category === cat);
  });

  formatTitle = formatMetricTitle;
  formatValue = formatMetricValue;
  isPositiveUp = isPositiveUpMetric;

  trendIcon(trend: string): string {
    const t = (trend || '').toLowerCase();
    if (t.includes('rise')) {
      return 'trending_up';
    }

    if (t.includes('fall')) {
      return 'trending_down';
    }

    return 'trending_flat';
  }

  trendClass(summary: MetricSummary): string {
    const t = (summary.trendDirection || '').toLowerCase();
    const posUp = this.isPositiveUp(summary.metricName);
    if (t.includes('stable')) {
      return 'trend-neutral';
    }

    if (t.includes('rise')) {
      return posUp ? 'trend-good' : 'trend-bad';
    }

    if (t.includes('fall')) {
      return posUp ? 'trend-bad' : 'trend-good';
    }

    return 'trend-neutral';
  }

  onCardClick(metricName: string): void {
    this.cardSelected.emit(metricName);
  }

  isSelected(name: string): boolean {
    return this.selectedMetricName() === name;
  }
}
