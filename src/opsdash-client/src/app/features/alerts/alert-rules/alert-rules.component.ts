import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleChange, MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleChange, MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { of } from 'rxjs';
import { catchError, finalize, take } from 'rxjs/operators';
import {
  ConfirmDialogComponent,
  type ConfirmDialogData,
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import type { AlertHistoryRow, AlertRuleRow } from '../models/alert.models';
import { AlertRuleFormComponent, type AlertRuleFormDialogData } from '../alert-rule-form/alert-rule-form.component';
import { AlertsService } from '../alerts.service';

@Component({
  selector: 'app-alert-rules',
  standalone: true,
  imports: [
    MatTabsModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatButtonToggleModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatPaginatorModule,
    MatSortModule,
    MatSlideToggleModule,
    MatSnackBarModule,
    MatChipsModule,
    DatePipe,
    DecimalPipe,
  ],
  templateUrl: './alert-rules.component.html',
  styleUrl: './alert-rules.component.scss',
})
export class AlertRulesComponent {
  private readonly alerts = inject(AlertsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly rulesColumns = [
    'metricName',
    'operator',
    'alertMode',
    'threshold',
    'horizon',
    'isActive',
    'createdAt',
    'actions',
  ];
  readonly historyColumns = [
    'time',
    'metric',
    'mode',
    'values',
    'forecast',
    'operator',
    'threshold',
    'ack',
  ];

  readonly rulesLoading = signal(true);
  readonly historyLoading = signal(true);

  readonly rules = signal<AlertRuleRow[]>([]);
  readonly rulesTotal = signal(0);
  readonly rulesPageIndex = signal(0);
  readonly rulesPageSize = signal(20);
  readonly rulesSortActive = signal('createdAt');
  readonly rulesSortDirection = signal<'asc' | 'desc'>('desc');

  readonly historyRows = signal<AlertHistoryRow[]>([]);
  readonly historyTotal = signal(0);
  readonly historyPageIndex = signal(0);
  readonly historyPageSize = signal(20);
  readonly historyFilter = signal<'all' | 'current' | 'predictive'>('all');

  constructor() {
    this.loadRules();
    this.loadHistory();
  }

  onHistoryFilterChange(e: MatButtonToggleChange): void {
    const v = String(e.value ?? 'all') as 'all' | 'current' | 'predictive';
    if (v === 'all' || v === 'current' || v === 'predictive') {
      this.historyFilter.set(v);
      this.historyPageIndex.set(0);
      this.loadHistory();
    }
  }

  onRulesSort(sort: Sort): void {
    const dir: 'asc' | 'desc' = sort.direction === 'asc' ? 'asc' : 'desc';
    const active = sort.direction ? sort.active : 'createdAt';
    this.rulesSortActive.set(active);
    this.rulesSortDirection.set(sort.direction ? dir : 'desc');
    this.rulesPageIndex.set(0);
    this.loadRules();
  }

  onRulesPage(e: PageEvent): void {
    this.rulesPageIndex.set(e.pageIndex);
    this.rulesPageSize.set(e.pageSize);
    this.loadRules();
  }

  onHistoryPage(e: PageEvent): void {
    this.historyPageIndex.set(e.pageIndex);
    this.historyPageSize.set(e.pageSize);
    this.loadHistory();
  }

  openCreateRule(): void {
    this.dialog
      .open<AlertRuleFormComponent, AlertRuleFormDialogData, AlertRuleRow | undefined>(AlertRuleFormComponent, {
        width: '480px',
        maxWidth: '95vw',
        data: { mode: 'create' },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((created) => {
        if (created) {
          this.snackBar.open('Alert rule created', 'Dismiss', { duration: 3500 });
          this.loadRules();
        }
      });
  }

  openEditRule(row: AlertRuleRow): void {
    this.dialog
      .open<AlertRuleFormComponent, AlertRuleFormDialogData, AlertRuleRow | undefined>(AlertRuleFormComponent, {
        width: '480px',
        maxWidth: '95vw',
        data: { mode: 'edit', rule: row },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((updated) => {
        if (updated) {
          this.snackBar.open('Alert rule updated', 'Dismiss', { duration: 3500 });
          this.loadRules();
        }
      });
  }

  confirmDeleteRule(row: AlertRuleRow): void {
    this.dialog
      .open<ConfirmDialogComponent, ConfirmDialogData, boolean | undefined>(ConfirmDialogComponent, {
        width: '420px',
        data: {
          title: 'Delete alert rule',
          message: `Delete rule for metric “${row.metricName}”? This cannot be undone.`,
          confirmText: 'Delete',
        },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((ok) => {
        if (!ok) {
          return;
        }

        this.alerts
          .deleteAlertRule(row.id)
          .pipe(take(1))
          .subscribe((res) => {
            if (res.success) {
              this.snackBar.open('Alert rule deleted', 'Dismiss', { duration: 3500 });
              this.loadRules();
            } else {
              this.snackBar.open(res.message ?? 'Delete failed', 'Dismiss', { duration: 5000 });
            }
          });
      });
  }

  onActiveToggle(row: AlertRuleRow, ev: MatSlideToggleChange): void {
    const next = ev.checked;
    if (next === row.isActive) {
      return;
    }

    this.alerts
      .updateAlertRule(row.id, { isActive: next })
      .pipe(take(1))
      .subscribe((res) => {
        if (res.success) {
          this.snackBar.open(next ? 'Rule activated' : 'Rule deactivated', 'Dismiss', { duration: 3000 });
          this.loadRules();
        } else {
          this.snackBar.open(res.message ?? 'Update failed', 'Dismiss', { duration: 5000 });
          ev.source.checked = row.isActive;
        }
      });
  }

  acknowledge(row: AlertHistoryRow): void {
    this.alerts
      .acknowledgeAlert(row.id)
      .pipe(take(1))
      .subscribe((res) => {
        if (res.success) {
          this.snackBar.open('Alert acknowledged', 'Dismiss', { duration: 3500 });
          this.loadHistory();
        } else {
          this.snackBar.open(res.message ?? 'Acknowledge failed', 'Dismiss', { duration: 5000 });
        }
      });
  }

  isAcked(row: AlertHistoryRow): boolean {
    return !!row.acknowledgedAt;
  }

  private loadRules(): void {
    this.rulesLoading.set(true);
    this.alerts
      .getAlertRules(
        this.rulesPageIndex() + 1,
        this.rulesPageSize(),
        this.rulesSortActive(),
        this.rulesSortDirection(),
      )
      .pipe(catchError(() => of(null)), finalize(() => this.rulesLoading.set(false)), take(1))
      .subscribe((rules) => {
        if (rules?.success && rules.data) {
          this.rules.set(rules.data.items);
          this.rulesTotal.set(rules.data.totalCount);
        } else {
          this.rules.set([]);
          this.rulesTotal.set(0);
        }
      });
  }

  private loadHistory(): void {
    this.historyLoading.set(true);
    let pred: boolean | null = null;
    const f = this.historyFilter();
    if (f === 'predictive') {
      pred = true;
    } else if (f === 'current') {
      pred = false;
    }
    this.alerts
      .getAlerts(this.historyPageIndex() + 1, this.historyPageSize(), pred)
      .pipe(catchError(() => of(null)), finalize(() => this.historyLoading.set(false)), take(1))
      .subscribe((hist) => {
        if (hist?.success && hist.data) {
          this.historyRows.set(hist.data.items);
          this.historyTotal.set(hist.data.totalCount);
        } else {
          this.historyRows.set([]);
          this.historyTotal.set(0);
        }
      });
  }
}
