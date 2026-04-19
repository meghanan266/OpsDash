import { Component, effect, input, signal } from '@angular/core';
import { NgChartsModule } from 'ng2-charts';
import type { ChartConfiguration, ChartData } from 'chart.js';
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

      const primary = '#1976d2';
      const scatterPts = anoms.map((a) => {
        let bestI = 0;
        let best = Infinity;
        history.forEach((h, i) => {
          const d = Math.abs(Date.parse(h.recordedAt) - Date.parse(a.detectedAt));
          if (d < best) {
            best = d;
            bestI = i;
          }
        });
        return { x: labels[bestI] ?? labels[0], y: Number(a.metricValue) };
      });

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
            pointRadius: 0,
            pointHoverRadius: 4,
          },
          ...(scatterPts.length
            ? [
                {
                  type: 'scatter' as const,
                  label: 'Anomalies',
                  data: scatterPts,
                  pointBackgroundColor: '#c62828',
                  pointBorderColor: '#fff',
                  pointRadius: 5,
                  pointHoverRadius: 7,
                },
              ]
            : []),
        ],
      } as ChartData<'line'>);

      this.chartOptions.set(this.buildOptions(formatMetricTitle(name)));
    });
  }

  private shortDate(iso: string): string {
    const d = new Date(iso);
    return d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit' });
  }

  private buildOptions(title?: string): ChartConfiguration<'line'>['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: 'index', intersect: false },
      plugins: {
        legend: { display: true, position: 'top' },
        title: title ? { display: true, text: title } : { display: false },
        tooltip: {
          callbacks: {
            label: (ctx) => {
              const v = ctx.parsed.y;
              return `${ctx.dataset.label ?? ''}: ${v != null ? Number(v).toLocaleString() : ''}`;
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
