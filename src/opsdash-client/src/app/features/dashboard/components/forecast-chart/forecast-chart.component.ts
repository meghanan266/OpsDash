import { Component, effect, input, signal } from '@angular/core';
import { NgChartsModule } from 'ng2-charts';
import type { ChartConfiguration, ChartData } from 'chart.js';
import type { ForecastPoint, MetricHistoryPoint } from '../../models/dashboard.models';
import { formatMetricTitle } from '../../utils/metric-format';

@Component({
  selector: 'app-forecast-chart',
  standalone: true,
  imports: [NgChartsModule],
  templateUrl: './forecast-chart.component.html',
  styleUrl: './forecast-chart.component.scss',
})
export class ForecastChartComponent {
  readonly metricName = input<string>('');
  readonly historyData = input<MetricHistoryPoint[]>([]);
  readonly forecastData = input<ForecastPoint[] | undefined>(undefined);

  readonly chartType = signal<'line'>('line');
  readonly chartData = signal<ChartData<'line'>>({ labels: [], datasets: [] });
  readonly chartOptions = signal<ChartConfiguration<'line'>['options']>(this.buildOptions(''));

  constructor() {
    effect(() => {
      const name = this.metricName();
      const history = this.historyData();
      const forecast = this.forecastData() ?? [];

      const histLabels = history.map((h) => this.shortDate(h.recordedAt));
      const histValues = history.map((h) => Number(h.metricValue));

      const fcLabels = forecast.map((f) => this.shortDate(f.recordedAt));
      const fcValues = forecast.map((f) => Number(f.forecastValue));

      const labels = [...histLabels];
      for (const lab of fcLabels) {
        if (!labels.includes(lab)) {
          labels.push(lab);
        }
      }

      const histSeries = labels.map((lab) => {
        const i = histLabels.indexOf(lab);
        return i >= 0 ? histValues[i]! : null;
      });

      const datasets: ChartData<'line'>['datasets'] = [
        {
          type: 'line',
          label: 'Actual',
          data: histSeries,
          borderColor: '#1976d2',
          backgroundColor: 'rgba(25, 118, 210, 0.1)',
          fill: true,
          tension: 0.35,
          spanGaps: false,
        },
      ];

      if (forecast.length > 0) {
        const fcSeries = labels.map((lab) => {
          const i = fcLabels.indexOf(lab);
          return i >= 0 ? fcValues[i]! : null;
        });
        datasets.push({
          type: 'line',
          label: 'Forecast',
          data: fcSeries,
          borderColor: '#00897b',
          borderDash: [6, 4],
          fill: false,
          tension: 0.25,
          spanGaps: true,
          pointRadius: 3,
        });

        const hasBounds = forecast.some((f) => f.lowerBound != null && f.upperBound != null);
        if (hasBounds) {
          const low = labels.map((lab) => {
            const i = fcLabels.indexOf(lab);
            return i >= 0 && forecast[i]!.lowerBound != null ? Number(forecast[i]!.lowerBound) : null;
          });
          const high = labels.map((lab) => {
            const i = fcLabels.indexOf(lab);
            return i >= 0 && forecast[i]!.upperBound != null ? Number(forecast[i]!.upperBound) : null;
          });
          datasets.push({
            type: 'line',
            label: 'Range low',
            data: low,
            borderColor: 'rgba(0, 137, 123, 0.35)',
            pointRadius: 0,
            fill: false,
          });
          datasets.push({
            type: 'line',
            label: 'Range high',
            data: high,
            borderColor: 'rgba(0, 137, 123, 0.35)',
            pointRadius: 0,
            fill: '-2',
            backgroundColor: 'rgba(0, 137, 123, 0.08)',
          });
        }
      }

      this.chartData.set({ labels, datasets } as ChartData<'line'>);
      this.chartOptions.set(this.buildOptions(`${formatMetricTitle(name)} · forecast`));
    });
  }

  private shortDate(iso: string): string {
    const d = new Date(iso);
    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }

  private buildOptions(title: string): ChartConfiguration<'line'>['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: true, position: 'top' },
        title: title ? { display: true, text: title } : { display: false },
      },
      scales: {
        x: { ticks: { maxTicksLimit: 14 } },
        y: { beginAtZero: false, grid: { color: 'rgba(0,0,0,0.06)' } },
      },
    };
  }
}
