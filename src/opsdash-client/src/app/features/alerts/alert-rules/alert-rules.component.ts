import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonToggleChange, MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { forkJoin, of, Subject } from 'rxjs';
import { catchError, finalize, switchMap } from 'rxjs/operators';
import type { AlertHistoryRow, AlertRuleRow } from '../models/alert.models';
import { AlertsService } from '../alerts.service';

@Component({
  selector: 'app-alert-rules',
  standalone: true,
  imports: [
    MatTabsModule,
    MatCardModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatButtonToggleModule,
    DatePipe,
    DecimalPipe,
  ],
  templateUrl: './alert-rules.component.html',
  styleUrl: './alert-rules.component.scss',
})
export class AlertRulesComponent {
  private readonly alerts = inject(AlertsService);
  private readonly destroyRef = inject(DestroyRef);

  readonly rulesColumns = ['metricName', 'operator', 'threshold', 'mode', 'horizon', 'active', 'created'];
  readonly historyColumns = ['time', 'metric', 'mode', 'values', 'operator', 'threshold'];

  readonly loading = signal(true);
  readonly rules = signal<AlertRuleRow[]>([]);
  readonly historyRows = signal<AlertHistoryRow[]>([]);
  readonly historyFilter = signal<'all' | 'current' | 'predictive'>('all');

  private readonly reload$ = new Subject<void>();

  constructor() {
    this.reload$
      .pipe(
        switchMap(() => {
          this.loading.set(true);
          const pred =
            this.historyFilter() === 'all' ? null : this.historyFilter() === 'predictive' ? true : false;
          return forkJoin({
            rules: this.alerts.getAlertRules(1, 100).pipe(catchError(() => of(null))),
            hist: this.alerts.getAlerts(1, 100, pred).pipe(catchError(() => of(null))),
          }).pipe(finalize(() => this.loading.set(false)));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ rules, hist }) => {
        if (rules?.success && rules.data) {
          this.rules.set(rules.data.items);
        } else {
          this.rules.set([]);
        }

        if (hist?.success && hist.data) {
          this.historyRows.set(hist.data.items);
        } else {
          this.historyRows.set([]);
        }
      });

    this.reload$.next();
  }

  onHistoryFilterChange(e: MatButtonToggleChange): void {
    const v = String(e.value ?? 'all') as 'all' | 'current' | 'predictive';
    if (v === 'all' || v === 'current' || v === 'predictive') {
      this.historyFilter.set(v);
      this.reload$.next();
    }
  }
}
