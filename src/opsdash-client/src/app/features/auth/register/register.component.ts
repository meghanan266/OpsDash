import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { startWith } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';

function passwordMatchValidator(group: AbstractControl): ValidationErrors | null {
  const pass = group.get('password');
  const confirm = group.get('confirmPassword');
  if (!pass || !confirm || !confirm.value) {
    return null;
  }
  return pass.value === confirm.value ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly hidePassword = signal(true);
  readonly hideConfirmPassword = signal(true);

  readonly form = this.fb.nonNullable.group(
    {
      tenantName: ['', [Validators.required, Validators.maxLength(200)]],
      subdomain: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(/^[a-z0-9-]+$/)]],
      firstName: ['', [Validators.required, Validators.maxLength(100)]],
      lastName: ['', [Validators.required, Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordMatchValidator },
  );

  private readonly passwordValue = toSignal(this.form.controls.password.valueChanges.pipe(startWith(this.form.controls.password.value)), {
    initialValue: this.form.controls.password.value,
  });

  private readonly subdomainValue = toSignal(
    this.form.controls.subdomain.valueChanges.pipe(startWith(this.form.controls.subdomain.value)),
    { initialValue: this.form.controls.subdomain.value },
  );

  readonly hasUppercase = computed(() => /[A-Z]/.test(this.passwordValue() ?? ''));
  readonly hasLowercase = computed(() => /[a-z]/.test(this.passwordValue() ?? ''));
  readonly hasDigit = computed(() => /\d/.test(this.passwordValue() ?? ''));

  readonly subdomainPreview = computed(() => {
    const raw = (this.subdomainValue() ?? '').trim();
    const sub = raw.length > 0 ? raw : 'your-org';
    return `${sub}.opsdash.io`;
  });

  constructor() {
    this.form.controls.password.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.form.updateValueAndValidity({ emitEvent: false });
    });
  }

  togglePasswordVisibility(): void {
    this.hidePassword.update((v) => !v);
  }

  toggleConfirmPasswordVisibility(): void {
    this.hideConfirmPassword.update((v) => !v);
  }

  formatSubdomainField(): void {
    const control = this.form.controls.subdomain;
    const next = this.formatSubdomain(control.value);
    if (next !== control.value) {
      control.setValue(next, { emitEvent: true });
    }
  }

  private formatSubdomain(raw: string): string {
    let s = raw
      .toLowerCase()
      .trim()
      .replace(/\s+/g, '-')
      .replace(/[^a-z0-9-]/g, '-')
      .replace(/-+/g, '-')
      .replace(/^-+|-+$/g, '');
    if (s.length > 100) {
      s = s.slice(0, 100);
    }
    return s;
  }

  submit(): void {
    this.errorMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { tenantName, subdomain, firstName, lastName, email, password } = this.form.getRawValue();
    this.loading.set(true);

    this.auth
      .register({
        tenantName: tenantName.trim(),
        subdomain: subdomain.trim(),
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        email: email.trim(),
        password,
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          if (res.success && res.data) {
            const name = res.data.firstName?.trim() || 'there';
            this.notifications.success(`Welcome to OpsDash, ${name}!`);
            void this.router.navigate(['/dashboard']);
            return;
          }
          this.errorMessage.set(this.formatApiFailure(res.message, res.errors));
        },
        error: (err: unknown) => {
          this.errorMessage.set(this.extractHttpError(err));
        },
      });
  }

  passwordMismatchVisible(): boolean {
    const confirm = this.form.controls.confirmPassword;
    return !!confirm.touched && !!this.form.errors?.['passwordMismatch'];
  }

  private formatApiFailure(message: string | null, errors: string[] | null): string {
    const parts = [message, ...(errors ?? []).filter(Boolean)].filter(Boolean) as string[];
    return parts.length > 0 ? parts.join(' ') : 'Registration failed. Please try again.';
  }

  private extractHttpError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      const body = err.error;
      if (body && typeof body === 'object') {
        const msg = 'message' in body && body.message != null ? String(body.message) : null;
        const errs = 'errors' in body && Array.isArray(body.errors) ? (body.errors as unknown[]).filter(Boolean).map(String) : [];
        const combined = [msg, ...errs].filter(Boolean).join(' ');
        if (combined) {
          return combined;
        }
      }
      if (err.status === 0) {
        return 'Network error. Check your connection and try again.';
      }
      return err.message || 'Something went wrong. Please try again.';
    }
    return 'Something went wrong. Please try again.';
  }
}
