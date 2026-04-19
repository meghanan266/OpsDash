import { CommonModule, DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { debounceTime, distinctUntilChanged, finalize, take } from 'rxjs';
import type { User } from '../../../core/models/user.model';
import {
  ConfirmDialogComponent,
  type ConfirmDialogData,
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { UserFormComponent, type UserFormDialogData } from '../user-form/user-form.component';
import { UserService } from '../services/user.service';

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DatePipe,
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatDialogModule,
    MatProgressBarModule,
    MatSnackBarModule,
  ],
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.scss',
})
export class UserListComponent {
  private readonly userService = inject(UserService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  readonly displayedColumns: string[] = ['name', 'email', 'role', 'status', 'createdAt', 'actions'];

  readonly users = signal<User[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);

  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);

  readonly sortActive = signal<string>('createdAt');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');

  readonly searchControl = new FormControl('', { nonNullable: true });

  constructor() {
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.pageIndex.set(0);
        this.loadUsers();
      });

    this.loadUsers();
  }

  onSortChange(sort: Sort): void {
    const dir: 'asc' | 'desc' = sort.direction === 'asc' ? 'asc' : 'desc';
    const active = sort.direction ? sort.active : 'createdAt';
    this.sortActive.set(active);
    this.sortDirection.set(dir);
    this.pageIndex.set(0);
    this.loadUsers();
  }

  onPage(e: PageEvent): void {
    this.pageIndex.set(e.pageIndex);
    this.pageSize.set(e.pageSize);
    this.loadUsers();
  }

  openCreateDialog(): void {
    this.dialog
      .open<UserFormComponent, UserFormDialogData, User | undefined>(UserFormComponent, {
        width: '520px',
        maxWidth: '95vw',
        data: { mode: 'create' },
      })
      .afterClosed()
      .pipe(take(1))
      .subscribe((created) => {
        if (created) {
          this.snackBar.open('User created', 'Dismiss', { duration: 3500 });
          this.loadUsers();
        }
      });
  }

  openEditDialog(user: User): void {
    this.dialog
      .open<UserFormComponent, UserFormDialogData, User | undefined>(UserFormComponent, {
        width: '520px',
        maxWidth: '95vw',
        data: { mode: 'edit', user },
      })
      .afterClosed()
      .pipe(take(1))
      .subscribe((updated) => {
        if (updated) {
          this.snackBar.open('User updated', 'Dismiss', { duration: 3500 });
          this.loadUsers();
        }
      });
  }

  confirmDelete(user: User): void {
    this.dialog
      .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent,
        {
          width: '440px',
          maxWidth: '95vw',
          data: {
            title: 'Deactivate user',
            message: `Deactivate ${user.firstName} ${user.lastName} (${user.email})? They will no longer be able to sign in.`,
            confirmText: 'Deactivate',
          },
        },
      )
      .afterClosed()
      .pipe(take(1))
      .subscribe((ok) => {
        if (!ok) {
          return;
        }

        this.userService
          .deleteUser(user.id)
          .pipe(take(1))
          .subscribe((res) => {
            if (res.success) {
              this.snackBar.open('User deactivated', 'Dismiss', { duration: 3500 });
              this.loadUsers();
            } else {
              this.snackBar.open(res.message ?? 'Deactivate failed', 'Dismiss', { duration: 5000 });
            }
          });
      });
  }

  private loadUsers(): void {
    this.loading.set(true);
    this.userService
      .getUsers({
        searchTerm: this.searchControl.value.trim() || undefined,
        page: this.pageIndex() + 1,
        pageSize: this.pageSize(),
        sortBy: this.mapSortKey(this.sortActive()),
        sortDirection: this.sortDirection(),
      })
      .pipe(
        finalize(() => this.loading.set(false)),
        take(1),
      )
      .subscribe((res) => {
        if (res.success && res.data) {
          this.users.set(res.data.items);
          this.totalCount.set(res.data.totalCount);
        } else {
          this.users.set([]);
          this.totalCount.set(0);
          if (res.message) {
            this.snackBar.open(res.message, 'Dismiss', { duration: 5000 });
          }
        }
      });
  }

  private mapSortKey(active: string): string {
    switch (active) {
      case 'createdAt':
        return 'createdat';
      case 'name':
        return 'name';
      case 'email':
        return 'email';
      case 'role':
        return 'role';
      default:
        return 'createdat';
    }
  }
}
