import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonToggleChange, MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { forkJoin, of, Subject } from 'rxjs';
import { catchError, finalize, switchMap } from 'rxjs/operators';
import type { IncidentRow } from '../../dashboard/models/dashboard.models';
import type { IncidentStats } from '../models/incident.models';
import { IncidentsService } from '../incidents.service';

@Component({
  selector: 'app-incident-list',
  standalone: true,
  imports: [
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatButtonToggleModule,
    MatIconModule,
    RouterLink,
    DatePipe,
  ],
  templateUrl: './incident-list.component.html',
  styleUrl: './incident-list.component.scss',
})
export class IncidentListComponent {
  private readonly incidentsApi = inject(IncidentsService);
  private readonly destroyRef = inject(DestroyRef);

  readonly displayedColumns = ['severity', 'title', 'status', 'metrics', 'anomalyCount', 'duration', 'startedAt'];

  readonly loading = signal(true);
  readonly stats = signal<IncidentStats | null>(null);
  readonly rows = signal<IncidentRow[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);
  readonly statusFilter = signal<string | null>(null);
  readonly severityFilter = signal<string | null>(null);

  private readonly reload$ = new Subject<void>();

  constructor() {
    this.reload$
      .pipe(
        switchMap(() => {
          this.loading.set(true);
          return forkJoin({
            stats: this.incidentsApi.getStats().pipe(catchError(() => of(null))),
            list: this.incidentsApi
              .getIncidents(
                this.pageIndex() + 1,
                this.pageSize(),
                'startedAt',
                'desc',
                this.statusFilter(),
                this.severityFilter(),
              )
              .pipe(catchError(() => of(null))),
          }).pipe(finalize(() => this.loading.set(false)));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ stats, list }) => {
        if (stats?.success && stats.data) {
          this.stats.set(stats.data);
        } else {
          this.stats.set(null);
        }

        if (list?.success && list.data) {
          this.rows.set(list.data.items);
          this.totalCount.set(list.data.totalCount);
        } else {
          this.rows.set([]);
          this.totalCount.set(0);
        }
      });

    this.reload$.next();
  }

  onStatusChange(e: MatButtonToggleChange): void {
    const value = String(e.value ?? 'all');
    this.statusFilter.set(value === 'all' ? null : value);
    this.pageIndex.set(0);
    this.reload();
  }

  onSeverityChange(e: MatButtonToggleChange): void {
    const value = String(e.value ?? 'all');
    this.severityFilter.set(value === 'all' ? null : value);
    this.pageIndex.set(0);
    this.reload();
  }

  onPage(e: PageEvent): void {
    this.pageIndex.set(e.pageIndex);
    this.pageSize.set(e.pageSize);
    this.reload();
  }

  parseMetrics(json: string): string[] {
    if (!json?.trim()) {
      return [];
    }

    try {
      const v = JSON.parse(json) as unknown;
      return Array.isArray(v) ? v.map(String) : [];
    } catch {
      return [];
    }
  }

  duration(row: IncidentRow): string {
    const start = new Date(row.startedAt).getTime();
    const end = row.resolvedAt ? new Date(row.resolvedAt).getTime() : Date.now();
    const ms = Math.max(0, end - start);
    const s = Math.floor(ms / 1000);
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    if (h > 0) {
      return `${h}h ${m}m`;
    }

    if (m > 0) {
      return `${m}m`;
    }

    return '<1m';
  }

  severityIcon(sev: string): string {
    const s = (sev || '').toLowerCase();
    if (s.includes('severe')) {
      return 'priority_high';
    }

    if (s.includes('critical')) {
      return 'warning';
    }

    return 'info';
  }

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

  statusChipClass(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'open') {
      return 'st-open';
    }

    if (s === 'acknowledged') {
      return 'st-ack';
    }

    if (s === 'investigating') {
      return 'st-inv';
    }

    if (s === 'resolved') {
      return 'st-res';
    }

    return 'st-def';
  }

  private reload(): void {
    this.reload$.next();
  }
}
