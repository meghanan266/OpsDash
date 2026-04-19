import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { of, Subject, take } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import { AuditDiffDialogComponent, type AuditDiffDialogData } from './audit-diff-dialog.component';
import { AuditLogsService, type AuditLogRow } from '../../../core/services/audit-logs.service';

@Component({
  selector: 'app-audit-logs',
  standalone: true,
  imports: [
    FormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    DatePipe,
  ],
  templateUrl: './audit-logs.component.html',
  styleUrl: './audit-logs.component.scss',
})
export class AuditLogsComponent implements OnInit {
  private readonly api = inject(AuditLogsService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  private readonly reload$ = new Subject<void>();

  readonly displayedColumns: string[] = ['timestamp', 'user', 'action', 'entity', 'entityId', 'changes'];

  readonly entityOptions = [
    '',
    'Metric',
    'User',
    'Tenant',
    'AlertRule',
    'Incident',
    'HealthScore',
    'Report',
    'AnomalyScore',
  ];

  readonly loading = signal(true);
  readonly rows = signal<AuditLogRow[]>([]);
  readonly totalCount = signal(0);

  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);

  readonly entityFilter = signal('');
  readonly actionFilter = signal('');
  readonly startDate = signal('');
  readonly endDate = signal('');
  readonly userIdFilter = signal<number | null>(null);

  constructor() {
    this.reload$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load());
  }

  ngOnInit(): void {
    this.reload$.next();
  }

  applyFilters(): void {
    this.pageIndex.set(0);
    this.reload$.next();
  }

  onPage(e: PageEvent): void {
    this.pageIndex.set(e.pageIndex);
    this.pageSize.set(e.pageSize);
    this.reload$.next();
  }

  onUserIdInput(v: string | number | null): void {
    if (v === '' || v === null || v === undefined) {
      this.userIdFilter.set(null);
      return;
    }

    const n = typeof v === 'number' ? v : Number(v);
    this.userIdFilter.set(Number.isFinite(n) ? n : null);
  }

  openDiff(row: AuditLogRow): void {
    this.dialog.open<AuditDiffDialogComponent, AuditDiffDialogData>(AuditDiffDialogComponent, {
      data: { oldValues: row.oldValues, newValues: row.newValues },
      autoFocus: false,
    });
  }

  private load(): void {
    this.loading.set(true);
    const uid = this.userIdFilter();
    this.api
      .list({
        page: this.pageIndex() + 1,
        pageSize: this.pageSize(),
        entityName: this.entityFilter().trim() || undefined,
        action: this.actionFilter().trim() || undefined,
        startDate: this.toStartIso(this.startDate()),
        endDate: this.toEndIso(this.endDate()),
        userId: uid === null || uid === undefined || Number.isNaN(uid) ? undefined : uid,
      })
      .pipe(
        catchError(() => of(null)),
        finalize(() => this.loading.set(false)),
        take(1),
      )
      .subscribe((res) => {
        if (res?.success && res.data) {
          this.rows.set(res.data.items);
          this.totalCount.set(res.data.totalCount);
        } else {
          this.rows.set([]);
          this.totalCount.set(0);
          if (res && !res.success) {
            this.snackBar.open(res.message ?? 'Failed to load audit logs', 'Dismiss', { duration: 5000 });
          }
        }
      });
  }

  private toStartIso(d: string): string | undefined {
    const t = d.trim();
    if (!t) {
      return undefined;
    }

    return `${t}T00:00:00.000Z`;
  }

  private toEndIso(d: string): string | undefined {
    const t = d.trim();
    if (!t) {
      return undefined;
    }

    return `${t}T23:59:59.999Z`;
  }
}
