import { Component, computed, inject, model, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { Router, RouterLink } from '@angular/router';
import { combineLatest, forkJoin, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, finalize, skip, switchMap } from 'rxjs/operators';
import type { ApiResponse, HealthScore, PagedResult } from '../../core/models/common.model';
import { AnomalyFeedComponent } from './components/anomaly-feed/anomaly-feed.component';
import { DashboardFilterBarComponent } from './components/dashboard-filter-bar/dashboard-filter-bar.component';
import { DashboardMetricsTableComponent } from './components/dashboard-metrics-table/dashboard-metrics-table.component';
import { ForecastChartComponent } from './components/forecast-chart/forecast-chart.component';
import { HealthScoreComponent } from './components/health-score/health-score.component';
import { KpiSummaryComponent } from './components/kpi-summary/kpi-summary.component';
import { MetricChartComponent } from './components/metric-chart/metric-chart.component';
import type { Anomaly, IncidentRow, MetricHistoryPoint, MetricRow, MetricSummary } from './models/dashboard.models';
import { DashboardService } from './services/dashboard.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    MatCardModule,
    MatProgressBarModule,
    RouterLink,
    DashboardFilterBarComponent,
    HealthScoreComponent,
    KpiSummaryComponent,
    MetricChartComponent,
    ForecastChartComponent,
    AnomalyFeedComponent,
    DashboardMetricsTableComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  private readonly dashboard = inject(DashboardService);
  private readonly router = inject(Router);

  readonly filterStart = model<string>('');
  readonly filterEnd = model<string>('');
  readonly filterCategory = model<string | null>(null);

  readonly loading = signal(false);
  readonly categories = signal<string[]>([]);
  readonly summaries = signal<MetricSummary[]>([]);
  readonly healthScore = signal<HealthScore | null>(null);
  readonly anomalies = signal<Anomaly[]>([]);
  readonly incidents = signal<IncidentRow[]>([]);
  readonly chartMetricName = signal<string>('');
  readonly chartHistory = signal<MetricHistoryPoint[]>([]);
  readonly tableRows = signal<MetricRow[]>([]);
  readonly tableTotal = signal(0);
  readonly tablePageIndex = signal(0);
  readonly tablePageSize = signal(25);
  readonly tableSortActive = signal('recordedAt');
  readonly tableSortDirection = signal<'asc' | 'desc'>('desc');

  readonly chartAnomalies = computed(() => {
    const name = this.chartMetricName();
    return this.anomalies().filter((a) => a.metricName === name);
  });

  constructor() {
    this.reloadAll();

    toObservable(this.filterCategory)
      .pipe(distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        this.tablePageIndex.set(0);
        this.reloadSummaryTable();
      });

    combineLatest([toObservable(this.filterStart), toObservable(this.filterEnd)])
      .pipe(
        skip(1),
        debounceTime(500),
        distinctUntilChanged((a, b) => a[0] === b[0] && a[1] === b[1]),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.reloadDateBound());
  }

  onRefresh(): void {
    this.reloadAll();
  }

  onKpiSelect(metricName: string): void {
    this.chartMetricName.set(metricName);
    this.loadChartHistory();
  }

  onTablePage(e: PageEvent): void {
    this.tablePageIndex.set(e.pageIndex);
    this.tablePageSize.set(e.pageSize);
    this.loadTable();
  }

  onTableSort(s: Sort): void {
    if (!s.active || !s.direction) {
      return;
    }

    this.tableSortActive.set(s.active);
    this.tableSortDirection.set(s.direction);
    this.tablePageIndex.set(0);
    this.loadTable();
  }

  onAnomalyClick(_a: Anomaly): void {
    void this.router.navigate(['/incidents']);
  }

  private dateParams(): { startDate?: string; endDate?: string } {
    const s = this.filterStart().trim();
    const e = this.filterEnd().trim();
    return {
      startDate: s || undefined,
      endDate: e || undefined,
    };
  }

  private reloadAll(): void {
    this.loading.set(true);
    const d = this.dateParams();
    forkJoin({
      categories: this.dashboard.getCategories().pipe(catchError(() => of(null))),
      summary: this.dashboard.getSummary(d.startDate, d.endDate).pipe(catchError(() => of(null))),
      health: this.dashboard.getHealthScore().pipe(catchError(() => of(null))),
      anomalies: this.dashboard.getActiveAnomalies(1, 100).pipe(catchError(() => of(null))),
      incidents: this.dashboard.getRecentIncidents().pipe(catchError(() => of(null))),
    })
      .pipe(
        switchMap((res) => {
          if (res.categories?.success && res.categories.data) {
            this.categories.set(res.categories.data);
          }

          if (res.summary?.success && res.summary.data) {
            this.summaries.set(res.summary.data);
          } else {
            this.summaries.set([]);
          }

          if (res.health?.success) {
            this.healthScore.set(res.health.data ?? null);
          } else {
            this.healthScore.set(null);
          }

          if (res.anomalies?.success && res.anomalies.data) {
            this.anomalies.set(res.anomalies.data.items);
          } else {
            this.anomalies.set([]);
          }

          if (res.incidents?.success && res.incidents.data) {
            this.incidents.set(res.incidents.data.items);
          } else {
            this.incidents.set([]);
          }

          this.pickDefaultMetric();
          const name = this.chartMetricName();
          const cat = this.filterCategory() ?? undefined;
          return forkJoin({
            chart: name
              ? this.dashboard.getMetricHistory(name, d.startDate, d.endDate, 'raw').pipe(catchError(() => of(null)))
              : of(null),
            table: this.dashboard
              .getMetrics(
                cat,
                this.tablePageIndex() + 1,
                this.tablePageSize(),
                this.tableSortActive(),
                this.tableSortDirection(),
              )
              .pipe(catchError(() => of(null))),
          });
        }),
        finalize(() => this.loading.set(false)),
      )
      .subscribe((r2) => {
        if (r2.chart?.success && r2.chart.data) {
          this.chartHistory.set(r2.chart.data);
        } else {
          this.chartHistory.set([]);
        }

        if (r2.table?.success && r2.table.data) {
          this.tableRows.set(r2.table.data.items);
          this.tableTotal.set(r2.table.data.totalCount);
        } else {
          this.tableRows.set([]);
          this.tableTotal.set(0);
        }
      });
  }

  private reloadSummaryTable(): void {
    this.loading.set(true);
    const d = this.dateParams();
    this.dashboard
      .getSummary(d.startDate, d.endDate)
      .pipe(
        catchError(() => of(null)),
        switchMap((res) => {
          if (res?.success && res.data) {
            this.summaries.set(res.data);
          } else {
            this.summaries.set([]);
          }

          this.pickDefaultMetric();
          return forkJoin({
            chart: this.chartMetricName()
              ? this.dashboard.getMetricHistory(this.chartMetricName(), d.startDate, d.endDate, 'raw').pipe(
                  catchError(() => of(null)),
                )
              : of(null),
            table: this.loadTableRequest(),
          });
        }),
        finalize(() => this.loading.set(false)),
      )
      .subscribe((r2) => {
        if (r2.chart?.success && r2.chart.data) {
          this.chartHistory.set(r2.chart.data);
        } else {
          this.chartHistory.set([]);
        }

        this.applyTableResult(r2.table);
      });
  }

  private reloadDateBound(): void {
    this.loading.set(true);
    const d = this.dateParams();
    this.dashboard
      .getSummary(d.startDate, d.endDate)
      .pipe(
        catchError(() => of(null)),
        switchMap((res) => {
          if (res?.success && res.data) {
            this.summaries.set(res.data);
          }

          return forkJoin({
            chart: this.chartMetricName()
              ? this.dashboard.getMetricHistory(this.chartMetricName(), d.startDate, d.endDate, 'raw').pipe(
                  catchError(() => of(null)),
                )
              : of(null),
            table: this.loadTableRequest(),
          });
        }),
        finalize(() => this.loading.set(false)),
      )
      .subscribe((r2) => {
        if (r2.chart?.success && r2.chart.data) {
          this.chartHistory.set(r2.chart.data);
        } else {
          this.chartHistory.set([]);
        }

        this.applyTableResult(r2.table);
      });
  }

  private loadTableRequest() {
    const cat = this.filterCategory() ?? undefined;
    return this.dashboard
      .getMetrics(
        cat,
        this.tablePageIndex() + 1,
        this.tablePageSize(),
        this.tableSortActive(),
        this.tableSortDirection(),
      )
      .pipe(catchError(() => of(null)));
  }

  private applyTableResult(table: ApiResponse<PagedResult<MetricRow>> | null): void {
    if (table?.success && table.data) {
      this.tableRows.set(table.data.items);
      this.tableTotal.set(table.data.totalCount);
    } else {
      this.tableRows.set([]);
      this.tableTotal.set(0);
    }
  }

  private pickDefaultMetric(): void {
    const list = this.filteredSummaries();
    const current = this.chartMetricName();
    if (current && list.some((s) => s.metricName === current)) {
      return;
    }

    const first = list[0]?.metricName ?? this.summaries()[0]?.metricName ?? '';
    this.chartMetricName.set(first);
  }

  private filteredSummaries(): MetricSummary[] {
    const cat = this.filterCategory();
    const all = this.summaries();
    if (!cat) {
      return all;
    }

    return all.filter((s) => s.category === cat);
  }

  private loadChartHistory(): void {
    const name = this.chartMetricName();
    if (!name) {
      this.chartHistory.set([]);
      return;
    }

    const d = this.dateParams();
    this.dashboard.getMetricHistory(name, d.startDate, d.endDate, 'raw').subscribe((res) => {
      this.chartHistory.set(res.success && res.data ? res.data : []);
    });
  }

  private loadTable(): void {
    this.loadTableRequest().subscribe((res) => this.applyTableResult(res));
  }
}
