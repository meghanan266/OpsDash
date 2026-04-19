import { DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { catchError, finalize, switchMap, take } from 'rxjs/operators';
import type { IncidentDetail } from '../models/incident.models';
import { IncidentsService } from '../incidents.service';
import { DashboardRealtimeBridge } from '../../../core/services/dashboard-realtime.bridge';
import { ReportsService } from '../../../core/services/reports.service';
import { formatTimeAgo } from '../../dashboard/utils/time-ago';

@Component({
  selector: 'app-incident-detail',
  standalone: true,
  imports: [
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatSelectModule,
    FormsModule,
    MatTableModule,
    MatSnackBarModule,
    RouterLink,
    DatePipe,
    DecimalPipe,
    NgClass,
  ],
  templateUrl: './incident-detail.component.html',
  styleUrl: './incident-detail.component.scss',
})
export class IncidentDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly incidentsApi = inject(IncidentsService);
  private readonly reports = inject(ReportsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly realtimeBridge = inject(DashboardRealtimeBridge);

  readonly loading = signal(true);
  readonly exportBusy = signal(false);
  readonly incident = signal<IncidentDetail | null>(null);
  readonly statusEdit = signal('');
  readonly correlationColumns = ['metricName', 'value', 'zScore', 'offset'];

  timeAgo = formatTimeAgo;

  constructor() {
    this.route.paramMap
      .pipe(
        switchMap((pm) => {
          const id = Number(pm.get('id'));
          if (!Number.isFinite(id) || id <= 0) {
            return of(null);
          }

          this.loading.set(true);
          return this.incidentsApi.getById(id).pipe(
            catchError(() => of(null)),
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((res) => {
        if (res?.success && res.data) {
          this.incident.set(res.data);
          this.statusEdit.set(res.data.status);
        } else {
          this.incident.set(null);
          this.statusEdit.set('');
        }
      });

    this.realtimeBridge.incidentUpdated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((n) => {
      const cur = Number(this.route.snapshot.paramMap.get('id'));
      if (n.incidentId === cur) {
        this.reloadOne(n.incidentId);
      }
    });
  }

  eventDotClass(type: string): string {
    const t = (type || '').toLowerCase();
    if (t === 'anomalydetected') {
      return 'dot-anomaly';
    }

    if (t === 'correlationfound') {
      return 'dot-corr';
    }

    if (t === 'acknowledged') {
      return 'dot-ack';
    }

    if (t === 'statuschanged') {
      return 'dot-status';
    }

    if (t === 'resolved') {
      return 'dot-resolved';
    }

    if (t === 'metricnormalized') {
      return 'dot-norm';
    }

    return 'dot-default';
  }

  canAcknowledge(): boolean {
    const i = this.incident();
    return !!i && i.status.toLowerCase() === 'open';
  }

  acknowledge(): void {
    const i = this.incident();
    if (!i) {
      return;
    }

    this.incidentsApi
      .acknowledge(i.id)
      .pipe(catchError(() => of(null)), take(1))
      .subscribe((res) => {
        if (res?.success) {
          this.snackBar.open('Incident acknowledged', 'Dismiss', { duration: 4000 });
          this.reloadOne(i.id);
        } else {
          this.snackBar.open(res?.message ?? 'Acknowledge failed', 'Dismiss', { duration: 5000 });
        }
      });
  }

  applyStatus(): void {
    const i = this.incident();
    const st = this.statusEdit().trim();
    if (!i || !st) {
      return;
    }

    this.incidentsApi
      .updateStatus(i.id, st)
      .pipe(catchError(() => of(null)), take(1))
      .subscribe((res) => {
        if (res?.success) {
          this.snackBar.open('Status updated', 'Dismiss', { duration: 4000 });
          this.reloadOne(i.id);
        } else {
          this.snackBar.open(res?.message ?? 'Update failed', 'Dismiss', { duration: 5000 });
        }
      });
  }

  exportReport(): void {
    const i = this.incident();
    if (!i || this.exportBusy()) {
      return;
    }

    this.exportBusy.set(true);
    this.reports
      .downloadIncidentCsv(i.id)
      .pipe(finalize(() => this.exportBusy.set(false)), take(1))
      .subscribe({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `incident-${i.id}-report.csv`;
          a.rel = 'noopener';
          a.click();
          URL.revokeObjectURL(url);
          this.snackBar.open('Report downloaded', 'Dismiss', { duration: 3500 });
        },
        error: () => {
          this.snackBar.open('Export failed', 'Dismiss', { duration: 5000 });
        },
      });
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

  duration(inc: IncidentDetail): string {
    const start = new Date(inc.startedAt).getTime();
    const end = inc.resolvedAt ? new Date(inc.resolvedAt).getTime() : Date.now();
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

  severityChipClass(sev: string): string {
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

  formatOffset(sec: number): string {
    if (sec === 0) {
      return '0s';
    }

    if (Math.abs(sec) < 60) {
      return `${sec}s`;
    }

    return `${Math.round(sec / 60)}m`;
  }

  private reloadOne(id: number): void {
    this.loading.set(true);
    this.incidentsApi
      .getById(id)
      .pipe(
        catchError(() => of(null)),
        finalize(() => this.loading.set(false)),
        take(1),
      )
      .subscribe((res) => {
        if (res?.success && res.data) {
          this.incident.set(res.data);
          this.statusEdit.set(res.data.status);
        }
      });
  }
}
