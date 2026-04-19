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
  readonly chartOptions = signal<ChartConfiguration<'line'>['options']>(this.buildOptions('', []));

  constructor() {
    effect(() => {
      const name = this.metricName();
      const history = this.historyData();
      const forecast = this.forecastData() ?? [];

      const labels = this.buildSortedLabels(history, forecast);
      const histByIso = new Map(history.map((h) => [h.recordedAt, Number(h.metricValue)]));
      const fcByIso = new Map(forecast.map((f) => [f.forecastedFor, Number(f.forecastedValue)]));
      const lowByIso = new Map(
        forecast
          .filter((f) => f.confidenceLower != null)
          .map((f) => [f.forecastedFor, Number(f.confidenceLower)]),
      );
      const highByIso = new Map(
        forecast
          .filter((f) => f.confidenceUpper != null)
          .map((f) => [f.forecastedFor, Number(f.confidenceUpper)]),
      );

      const histSeries = labels.map((iso) => (histByIso.has(iso) ? histByIso.get(iso)! : null));
      const datasets: ChartData<'line'>['datasets'] = [
        {
          type: 'line',
          label: 'Actual',
          data: histSeries,
          borderColor: '#1976d2',
          backgroundColor: 'rgba(25, 118, 210, 0.08)',
          fill: true,
          tension: 0.35,
          spanGaps: false,
        },
      ];

      if (forecast.length > 0) {
        const fcSeries = labels.map((iso) => (fcByIso.has(iso) ? fcByIso.get(iso)! : null));
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

        const hasBounds = forecast.some((f) => f.confidenceLower != null && f.confidenceUpper != null);
        if (hasBounds) {
          const low = labels.map((iso) => (lowByIso.has(iso) ? lowByIso.get(iso)! : null));
          const high = labels.map((iso) => (highByIso.has(iso) ? highByIso.get(iso)! : null));
          datasets.push({
            type: 'line',
            label: 'Confidence lower',
            data: low,
            borderColor: 'rgba(0, 137, 123, 0.2)',
            backgroundColor: 'transparent',
            pointRadius: 0,
            fill: false,
            tension: 0.2,
            spanGaps: true,
          });
          datasets.push({
            type: 'line',
            label: 'Confidence band',
            data: high,
            borderColor: 'rgba(0, 137, 123, 0.35)',
            pointRadius: 0,
            fill: '-1',
            backgroundColor: 'rgba(0, 137, 123, 0.12)',
            tension: 0.2,
            spanGaps: true,
          });
        }
      }

      this.chartData.set({ labels, datasets } as ChartData<'line'>);
      this.chartOptions.set(this.buildOptions(`${formatMetricTitle(name)} · forecast`, labels));
    });
  }

  private buildSortedLabels(history: MetricHistoryPoint[], forecast: ForecastPoint[]): string[] {
    const seen = new Set<string>();
    const times: { t: number; iso: string }[] = [];
    for (const h of history) {
      if (!seen.has(h.recordedAt)) {
        seen.add(h.recordedAt);
        times.push({ t: new Date(h.recordedAt).getTime(), iso: h.recordedAt });
      }
    }
    for (const f of forecast) {
      if (!seen.has(f.forecastedFor)) {
        seen.add(f.forecastedFor);
        times.push({ t: new Date(f.forecastedFor).getTime(), iso: f.forecastedFor });
      }
    }
    times.sort((a, b) => a.t - b.t);
    return times.map((x) => x.iso);
  }

  private formatAxisLabel(iso: string): string {
    const d = new Date(iso);
    return d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  private buildOptions(title: string, categoryLabels: string[]): ChartConfiguration<'line'>['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: true,
          position: 'top',
          labels: {
            filter: (item) => item.text !== 'Confidence lower',
          },
        },
        title: title ? { display: true, text: title } : { display: false },
      },
      scales: {
        x: {
          ticks: {
            maxTicksLimit: 16,
            maxRotation: 45,
            callback: (tickValue) => {
              const idx = typeof tickValue === 'number' ? tickValue : Number(tickValue);
              const iso = categoryLabels[idx];
              return iso ? this.formatAxisLabel(iso) : '';
            },
          },
        },
        y: { beginAtZero: false, grid: { color: 'rgba(0,0,0,0.06)' } },
      },
    };
  }
}
