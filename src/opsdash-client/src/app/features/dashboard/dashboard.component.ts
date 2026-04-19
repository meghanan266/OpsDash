import { Component, computed, DestroyRef, inject, model, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatButtonToggleChange, MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { combineLatest, forkJoin, of, take } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, finalize, skip, switchMap } from 'rxjs/operators';
import type { ApiResponse, PagedResult } from '../../core/models/common.model';
import { AnomalyFeedComponent } from './components/anomaly-feed/anomaly-feed.component';
import { DashboardFilterBarComponent } from './components/dashboard-filter-bar/dashboard-filter-bar.component';
import { DashboardMetricsTableComponent } from './components/dashboard-metrics-table/dashboard-metrics-table.component';
import { ForecastChartComponent } from './components/forecast-chart/forecast-chart.component';
import { HealthScoreComponent } from './components/health-score/health-score.component';
import { KpiSummaryComponent } from './components/kpi-summary/kpi-summary.component';
import { MetricChartComponent } from './components/metric-chart/metric-chart.component';
import { RecentIncidentsPanelComponent } from './components/recent-incidents-panel/recent-incidents-panel.component';
import type { AnomalyNotification, IncidentNotification } from '../../core/models/notification.model';
import { DashboardRealtimeBridge } from '../../core/services/dashboard-realtime.bridge';
import type { Anomaly, ForecastPoint, IncidentRow, MetricHistoryPoint, MetricRow, MetricSummary } from './models/dashboard.models';
import { DashboardService } from './services/dashboard.service';
import { ReportsService } from '../../core/services/reports.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    MatCardModule,
    MatButtonToggleModule,
    MatSnackBarModule,
    MatProgressBarModule,
    DashboardFilterBarComponent,
    HealthScoreComponent,
    KpiSummaryComponent,
    MetricChartComponent,
    ForecastChartComponent,
    AnomalyFeedComponent,
    RecentIncidentsPanelComponent,
    DashboardMetricsTableComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  private readonly dashboard = inject(DashboardService);
  private readonly reports = inject(ReportsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly realtimeBridge = inject(DashboardRealtimeBridge);
  private readonly destroyRef = inject(DestroyRef);

  readonly filterStart = model<string>('');
  readonly filterEnd = model<string>('');
  readonly filterCategory = model<string | null>(null);

  readonly loading = signal(false);
  readonly exportBusy = signal(false);
  readonly categories = signal<string[]>([]);
  readonly summaries = signal<MetricSummary[]>([]);
  readonly anomalies = signal<Anomaly[]>([]);
  readonly incidents = signal<IncidentRow[]>([]);
  readonly chartMetricName = signal<string>('');
  readonly chartHistory = signal<MetricHistoryPoint[]>([]);
  readonly chartForecast = signal<ForecastPoint[]>([]);
  readonly chartAnomalies = signal<Anomaly[]>([]);
  readonly forecastMethod = signal<'WMA' | 'LinearRegression'>('WMA');
  readonly tableRows = signal<MetricRow[]>([]);
  readonly tableTotal = signal(0);
  readonly tablePageIndex = signal(0);
  readonly tablePageSize = signal(25);
  readonly tableSortActive = signal('recordedAt');
  readonly tableSortDirection = signal<'asc' | 'desc'>('desc');

  readonly hasChartMetric = computed(() => !!this.chartMetricName().trim());

  constructor() {
    this.reloadAll();

    this.realtimeBridge.anomalyDetected$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((n) => {
      const row = this.toAnomalyRow(n);
      this.anomalies.update((list) => (list.some((a) => a.id === row.id) ? list : [row, ...list]));
    });

    this.realtimeBridge.incidentCreated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((n) => {
      this.upsertIncidentRow(this.toIncidentRow(n));
    });

    this.realtimeBridge.incidentUpdated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((n) => {
      this.upsertIncidentRow(this.toIncidentRow(n));
    });

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

  onExportCsv(): void {
    if (this.exportBusy()) {
      return;
    }

    this.exportBusy.set(true);
    const d = this.dateParams();
    this.reports
      .downloadDashboardCsv(d.startDate, d.endDate)
      .pipe(finalize(() => this.exportBusy.set(false)), take(1))
      .subscribe({
        next: (blob) => {
          this.triggerFileDownload(blob, 'dashboard-metrics.csv');
          this.snackBar.open('Report downloaded', 'Dismiss', { duration: 3500 });
        },
        error: () => {
          this.snackBar.open('Export failed', 'Dismiss', { duration: 5000 });
        },
      });
  }

  private triggerFileDownload(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.rel = 'noopener';
    a.click();
    URL.revokeObjectURL(url);
  }

  onKpiSelect(metricName: string): void {
    this.chartMetricName.set(metricName);
    this.loadChartHistory();
  }

  onForecastMethodChange(e: MatButtonToggleChange): void {
    const v = e.value as 'WMA' | 'LinearRegression';
    if (v === 'WMA' || v === 'LinearRegression') {
      this.forecastMethod.set(v);
      this.loadChartHistory();
    }
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
            forecast: name
              ? this.dashboard
                  .getMetricForecast(name, this.forecastMethod(), undefined)
                  .pipe(catchError(() => of(null)))
              : of(null),
            markers: name
              ? this.dashboard.getAnomaliesForMetric(name, 1, 300).pipe(catchError(() => of(null)))
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

        if (r2.forecast?.success && r2.forecast.data) {
          this.chartForecast.set(r2.forecast.data);
        } else {
          this.chartForecast.set([]);
        }

        if (r2.markers?.success && r2.markers.data) {
          this.chartAnomalies.set(r2.markers.data.items);
        } else {
          this.chartAnomalies.set([]);
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
            forecast: this.chartMetricName()
              ? this.dashboard
                  .getMetricForecast(this.chartMetricName(), this.forecastMethod(), undefined)
                  .pipe(catchError(() => of(null)))
              : of(null),
            markers: this.chartMetricName()
              ? this.dashboard.getAnomaliesForMetric(this.chartMetricName(), 1, 300).pipe(catchError(() => of(null)))
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

        if (r2.forecast?.success && r2.forecast.data) {
          this.chartForecast.set(r2.forecast.data);
        } else {
          this.chartForecast.set([]);
        }

        if (r2.markers?.success && r2.markers.data) {
          this.chartAnomalies.set(r2.markers.data.items);
        } else {
          this.chartAnomalies.set([]);
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
            forecast: this.chartMetricName()
              ? this.dashboard
                  .getMetricForecast(this.chartMetricName(), this.forecastMethod(), undefined)
                  .pipe(catchError(() => of(null)))
              : of(null),
            markers: this.chartMetricName()
              ? this.dashboard.getAnomaliesForMetric(this.chartMetricName(), 1, 300).pipe(catchError(() => of(null)))
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

        if (r2.forecast?.success && r2.forecast.data) {
          this.chartForecast.set(r2.forecast.data);
        } else {
          this.chartForecast.set([]);
        }

        if (r2.markers?.success && r2.markers.data) {
          this.chartAnomalies.set(r2.markers.data.items);
        } else {
          this.chartAnomalies.set([]);
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
      this.chartForecast.set([]);
      this.chartAnomalies.set([]);
      return;
    }

    const d = this.dateParams();
    forkJoin({
      chart: this.dashboard.getMetricHistory(name, d.startDate, d.endDate, 'raw').pipe(catchError(() => of(null))),
      forecast: this.dashboard
        .getMetricForecast(name, this.forecastMethod(), undefined)
        .pipe(catchError(() => of(null))),
      markers: this.dashboard.getAnomaliesForMetric(name, 1, 300).pipe(catchError(() => of(null))),
    }).subscribe((r) => {
      this.chartHistory.set(r.chart?.success && r.chart.data ? r.chart.data : []);
      this.chartForecast.set(r.forecast?.success && r.forecast.data ? r.forecast.data : []);
      this.chartAnomalies.set(r.markers?.success && r.markers.data ? r.markers.data.items : []);
    });
  }

  private loadTable(): void {
    this.loadTableRequest().subscribe((res) => this.applyTableResult(res));
  }

  private upsertIncidentRow(row: IncidentRow): void {
    this.incidents.update((list) => {
      const others = list.filter((i) => i.id !== row.id);
      return [row, ...others].slice(0, 5);
    });
  }

  private toAnomalyRow(n: AnomalyNotification): Anomaly {
    return {
      id: n.anomalyId,
      metricName: n.metricName,
      metricValue: n.metricValue,
      zScore: n.zScore,
      severity: n.severity,
      detectedAt: this.toIso(n.detectedAt),
      isActive: true,
      resolvedAt: null,
      incidentId: n.incidentId,
    };
  }

  private toIncidentRow(n: IncidentNotification): IncidentRow {
    return {
      id: n.incidentId,
      title: n.title,
      severity: n.severity,
      status: n.status,
      anomalyCount: n.anomalyCount,
      affectedMetrics: n.affectedMetrics,
      startedAt: this.toIso(n.startedAt),
      acknowledgedAt: null,
      resolvedAt: null,
    };
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
}
