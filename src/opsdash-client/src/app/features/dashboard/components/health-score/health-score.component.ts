import { DecimalPipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { NgChartsModule } from 'ng2-charts';
import type { ChartConfiguration, ChartData } from 'chart.js';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import type { HealthScore } from '../../../../core/models/common.model';
import type { HealthScoreNotification } from '../../../../core/models/notification.model';
import { DashboardRealtimeBridge } from '../../../../core/services/dashboard-realtime.bridge';
import { DashboardService } from '../../services/dashboard.service';

@Component({
  selector: 'app-health-score',
  standalone: true,
  imports: [MatCardModule, MatProgressSpinnerModule, DecimalPipe, NgChartsModule],
  templateUrl: './health-score.component.html',
  styleUrl: './health-score.component.scss',
})
export class HealthScoreComponent {
  private readonly dashboard = inject(DashboardService);
  private readonly bridge = inject(DashboardRealtimeBridge);
  private readonly destroyRef = inject(DestroyRef);

  /** SVG circle r=52 */
  readonly circumference = 2 * Math.PI * 52;

  readonly loading = signal(true);
  readonly healthScore = signal<HealthScore | null>(null);
  readonly historyScores = signal<HealthScore[]>([]);

  readonly sparklineType = signal<'line'>('line');
  readonly sparklineData = signal<ChartData<'line'>>({ labels: [], datasets: [] });
  readonly sparklineOptions = signal<ChartConfiguration<'line'>['options']>(this.buildSparkOptions());

  readonly score = computed(() => {
    const h = this.healthScore();
    return h ? Math.round(h.overallScore) : null;
  });

  readonly bandClass = computed(() => {
    const s = this.score();
    if (s === null) {
      return 'band-unknown';
    }

    if (s >= 80) {
      return 'band-high';
    }

    if (s >= 50) {
      return 'band-mid';
    }

    return 'band-low';
  });

  readonly dashOffset = computed(() => {
    const s = this.score();
    if (s === null) {
      return this.circumference;
    }

    const pct = Math.max(0, Math.min(100, s));
    return this.circumference * (1 - pct / 100);
  });

  constructor() {
    this.loading.set(true);
    forkJoin({
      latest: this.dashboard.getHealthScore().pipe(catchError(() => of(null))),
      history: this.dashboard.getHealthScoreHistory(7).pipe(catchError(() => of(null))),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ latest, history }) => {
        if (latest?.success) {
          this.healthScore.set(latest.data ?? null);
        } else {
          this.healthScore.set(null);
        }

        if (history?.success && history.data?.length) {
          const sorted = [...history.data].sort(
            (a, b) => new Date(a.calculatedAt).getTime() - new Date(b.calculatedAt).getTime(),
          );
          this.historyScores.set(sorted);
          this.updateSparkline(sorted);
        } else {
          this.historyScores.set([]);
          this.sparklineData.set({ labels: [], datasets: [] });
        }

        this.loading.set(false);
      });

    this.bridge.healthScoreUpdated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((n) => {
      this.applyHealthScorePatch(n);
    });
  }

  private applyHealthScorePatch(n: HealthScoreNotification): void {
    const calculatedAt = this.toIso(n.calculatedAt);
    const cur = this.healthScore();
    this.healthScore.set({
      id: cur?.id ?? 0,
      overallScore: Number(n.overallScore),
      normalMetricPct: Number(n.normalMetricPct),
      activeAnomalies: n.activeAnomalies,
      trendScore: cur?.trendScore ?? 0,
      responseScore: cur?.responseScore ?? 0,
      calculatedAt,
    });

    const synthetic: HealthScore = {
      id: 0,
      overallScore: Number(n.overallScore),
      normalMetricPct: Number(n.normalMetricPct),
      activeAnomalies: n.activeAnomalies,
      trendScore: cur?.trendScore ?? 0,
      responseScore: cur?.responseScore ?? 0,
      calculatedAt,
    };

    const merged = [...this.historyScores(), synthetic]
      .sort((a, b) => new Date(a.calculatedAt).getTime() - new Date(b.calculatedAt).getTime())
      .slice(-7);
    this.historyScores.set(merged);
    if (merged.length > 1) {
      this.updateSparkline(merged);
    }
  }

  private toIso(value: string | Date): string {
    if (value instanceof Date) {
      return value.toISOString();
    }

    if (typeof value === 'string') {
      return value;
    }

    return new Date(value as unknown as string).toISOString();
  }

  private updateSparkline(rows: HealthScore[]): void {
    const labels = rows.map((_, i) => String(i + 1));
    const values = rows.map((r) => Number(r.overallScore));
    this.sparklineData.set({
      labels,
      datasets: [
        {
          data: values,
          borderColor: '#1976d2',
          backgroundColor: 'rgba(25, 118, 210, 0.15)',
          fill: true,
          tension: 0.35,
          pointRadius: 2,
          borderWidth: 2,
        },
      ],
    });
  }

  private buildSparkOptions(): ChartConfiguration<'line'>['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false }, tooltip: { enabled: true } },
      scales: {
        x: { display: false },
        y: { display: false, min: 0, max: 100 },
      },
    };
  }
}
