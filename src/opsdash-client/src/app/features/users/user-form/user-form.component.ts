import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { take } from 'rxjs';
import type { User } from '../../../core/models/user.model';
import { UserService, type RoleDto } from '../services/user.service';

export interface UserFormDialogData {
  mode: 'create' | 'edit';
  user?: User;
}

@Component({
  selector: 'app-user-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './user-form.component.html',
  styleUrl: './user-form.component.scss',
})
export class UserFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly userService = inject(UserService);
  readonly dialogRef = inject(MatDialogRef<UserFormComponent, User | undefined>);
  readonly data = inject<UserFormDialogData>(MAT_DIALOG_DATA);

  readonly roles = signal<RoleDto[]>([]);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: [''],
    roleId: [0, [Validators.required, Validators.min(1)]],
    isActive: [true],
  });

  ngOnInit(): void {
    const pwd = this.form.controls.password;
    const email = this.form.controls.email;
    const isActive = this.form.controls.isActive;

    if (this.data.mode === 'create') {
      pwd.setValidators([Validators.required, Validators.minLength(8)]);
      isActive.disable({ emitEvent: false });
    } else {
      pwd.clearValidators();
      pwd.disable({ emitEvent: false });
      email.disable({ emitEvent: false });
      const u = this.data.user;
      if (u) {
        this.form.patchValue({
          firstName: u.firstName,
          lastName: u.lastName,
          email: u.email,
          roleId: u.roleId,
          isActive: u.isActive,
        });
      }
    }

    pwd.updateValueAndValidity({ emitEvent: false });

    this.userService
      .getRoles()
      .pipe(take(1))
      .subscribe((res) => {
        if (res.success && res.data?.length) {
          this.roles.set(res.data);
          if (this.data.mode === 'create' && this.form.controls.roleId.value < 1) {
            this.form.patchValue({ roleId: res.data[0].id });
          }
        } else {
          this.errorMessage.set(res.message ?? 'Could not load roles');
        }
      });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  submit(): void {
    this.errorMessage.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const v = this.form.getRawValue();

    if (this.data.mode === 'create') {
      this.userService
        .createUser({
          email: v.email.trim(),
          password: v.password,
          firstName: v.firstName.trim(),
          lastName: v.lastName.trim(),
          roleId: v.roleId,
        })
        .pipe(take(1))
        .subscribe({
          next: (res) => {
            this.saving.set(false);
            if (res.success && res.data) {
              this.dialogRef.close(res.data);
            } else {
              this.errorMessage.set(res.message ?? 'Create failed');
            }
          },
          error: () => {
            this.saving.set(false);
            this.errorMessage.set('Create failed');
          },
        });
      return;
    }

    const u = this.data.user;
    if (!u) {
      this.saving.set(false);
      return;
    }

    const body: {
      firstName?: string;
      lastName?: string;
      roleId?: number;
      isActive?: boolean;
    } = {};

    if (v.firstName.trim() !== u.firstName) {
      body.firstName = v.firstName.trim();
    }

    if (v.lastName.trim() !== u.lastName) {
      body.lastName = v.lastName.trim();
    }

    if (v.roleId !== u.roleId) {
      body.roleId = v.roleId;
    }

    if (v.isActive !== u.isActive) {
      body.isActive = v.isActive;
    }

    if (Object.keys(body).length === 0) {
      this.saving.set(false);
      this.dialogRef.close(u);
      return;
    }

    this.userService
      .updateUser(u.id, body)
      .pipe(take(1))
      .subscribe({
        next: (res) => {
          this.saving.set(false);
          if (res.success && res.data) {
            this.dialogRef.close(res.data);
          } else {
            this.errorMessage.set(res.message ?? 'Update failed');
          }
        },
        error: () => {
          this.saving.set(false);
          this.errorMessage.set('Update failed');
        },
      });
  }
}
