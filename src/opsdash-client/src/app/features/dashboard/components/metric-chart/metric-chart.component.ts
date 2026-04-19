import { Component, effect, input, signal } from '@angular/core';
import { NgChartsModule } from 'ng2-charts';
import type { ChartConfiguration, ChartData, TooltipItem } from 'chart.js';
import type { Anomaly, MetricHistoryPoint } from '../../models/dashboard.models';
import { formatMetricTitle } from '../../utils/metric-format';

@Component({
  selector: 'app-metric-chart',
  standalone: true,
  imports: [NgChartsModule],
  templateUrl: './metric-chart.component.html',
  styleUrl: './metric-chart.component.scss',
})
export class MetricChartComponent {
  readonly metricName = input<string>('');
  readonly historyData = input<MetricHistoryPoint[]>([]);
  readonly anomalies = input<Anomaly[] | undefined>(undefined);

  readonly chartType = signal<'line'>('line');

  readonly chartData = signal<ChartData<'line'>>({ labels: [], datasets: [] });

  readonly chartOptions = signal<ChartConfiguration<'line'>['options']>(this.buildOptions());

  constructor() {
    effect(() => {
      const name = this.metricName();
      const history = this.historyData();
      const anoms = this.anomalies() ?? [];

      const labels = history.map((h) => this.shortDate(h.recordedAt));
      const linePoints = history.map((h) => Number(h.metricValue));

      const anomalyByPointIndex: (Anomaly | null)[] = history.map((h) => {
        const t = Date.parse(h.recordedAt);
        let best: Anomaly | null = null;
        let bestDelta = Infinity;
        for (const a of anoms) {
          const d = Math.abs(Date.parse(a.detectedAt) - t);
          if (d < bestDelta && d < 120_000) {
            bestDelta = d;
            best = a;
          }
        }

        return bestDelta < 120_000 ? best : null;
      });

      const pointRadii = anomalyByPointIndex.map((a) => (a ? 6 : 0));
      const pointColors = anomalyByPointIndex.map((a) => (a ? '#c62828' : 'rgba(25,118,210,0)'));
      const pointBorderColors = anomalyByPointIndex.map((a) => (a ? '#fff' : 'transparent'));

      const primary = '#1976d2';

      this.chartData.set({
        labels,
        datasets: [
          {
            type: 'line',
            label: formatMetricTitle(name),
            data: linePoints,
            borderColor: primary,
            backgroundColor: 'rgba(25, 118, 210, 0.12)',
            fill: true,
            tension: 0.35,
            pointRadius: pointRadii,
            pointHoverRadius: pointRadii.map((r) => (r > 0 ? 8 : 4)),
            pointBackgroundColor: pointColors,
            pointBorderColor: pointBorderColors,
            pointBorderWidth: anomalyByPointIndex.map((a) => (a ? 2 : 0)),
          },
        ],
      });

      this.chartOptions.set(this.buildOptions(formatMetricTitle(name), anomalyByPointIndex));
    });
  }

  private shortDate(iso: string): string {
    const d = new Date(iso);
    return d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit' });
  }

  private buildOptions(
    title?: string,
    anomalyByPointIndex: (Anomaly | null)[] = [],
  ): ChartConfiguration<'line'>['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: 'index', intersect: false },
      plugins: {
        legend: { display: true, position: 'top' },
        title: title ? { display: true, text: title } : { display: false },
        tooltip: {
          callbacks: {
            label: (ctx: TooltipItem<'line'>) => {
              const v = ctx.parsed.y;
              const base = `${ctx.dataset.label ?? ''}: ${v != null ? Number(v).toLocaleString() : ''}`;
              const idx = ctx.dataIndex;
              const a = anomalyByPointIndex[idx];
              if (a) {
                const z = Number(a.zScore).toLocaleString(undefined, {
                  maximumFractionDigits: 2,
                  minimumFractionDigits: 2,
                });
                return `${base}\nAnomaly: Z-score ${z}, Severity: ${a.severity}`;
              }

              return base;
            },
          },
        },
      },
      scales: {
        x: {
          ticks: { maxRotation: 45, minRotation: 0, maxTicksLimit: 10 },
          grid: { display: false },
        },
        y: {
          beginAtZero: false,
          grid: { color: 'rgba(0,0,0,0.06)' },
        },
      },
    };
  }
}
