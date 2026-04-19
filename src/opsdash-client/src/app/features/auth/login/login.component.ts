import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs/operators';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
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
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly hidePassword = signal(true);

  readonly form = this.fb.nonNullable.group({
    subdomain: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  togglePasswordVisibility(): void {
    this.hidePassword.update((v) => !v);
  }

  submit(): void {
    this.errorMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { subdomain, email, password } = this.form.getRawValue();
    this.loading.set(true);

    this.auth
      .login({ subdomain: subdomain.trim(), email: email.trim(), password })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          if (res.success && res.data) {
            const name = res.data.firstName?.trim() || 'there';
            this.notifications.success(`Welcome back, ${name}!`);
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

  private formatApiFailure(message: string | null, errors: string[] | null): string {
    const parts = [message, ...(errors ?? []).filter(Boolean)].filter(Boolean) as string[];
    return parts.length > 0 ? parts.join(' ') : 'Sign in failed. Please try again.';
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
