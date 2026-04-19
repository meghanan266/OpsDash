import { Component, input, output } from '@angular/core';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import type { MetricRow } from '../../models/dashboard.models';
import { formatMetricTitle, formatMetricValue } from '../../utils/metric-format';

@Component({
  selector: 'app-dashboard-metrics-table',
  standalone: true,
  imports: [
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './dashboard-metrics-table.component.html',
  styleUrl: './dashboard-metrics-table.component.scss',
})
export class DashboardMetricsTableComponent {
  readonly displayedColumns = ['metricName', 'category', 'metricValue', 'recordedAt'];

  readonly rows = input<MetricRow[]>([]);
  readonly totalCount = input(0);
  readonly pageIndex = input(0);
  readonly pageSize = input(25);
  readonly loading = input(false);
  readonly categoryFilter = input<string | null>(null);
  readonly sortActive = input('recordedAt');
  readonly sortDirection = input<'asc' | 'desc'>('desc');

  readonly pageChange = output<PageEvent>();
  readonly sortChange = output<Sort>();

  formatTitle = formatMetricTitle;
  formatValue = formatMetricValue;

  onPage(e: PageEvent): void {
    this.pageChange.emit(e);
  }

  onSort(e: Sort): void {
    this.sortChange.emit(e);
  }

  displayValue(row: MetricRow): string {
    return formatMetricValue(row.metricName, row.metricValue);
  }

  displayRecorded(iso: string): string {
    return new Date(iso).toLocaleString();
  }
}
