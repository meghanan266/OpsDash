import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, of, tap } from 'rxjs';
import type { ApiResponse } from '../models/common.model';
import type { AuthResponse, LoginRequest, RegisterRequest } from '../models/user.model';
import { ApiService } from './api.service';

const TOKEN_KEY = 'opsdash_token';
const USER_KEY = 'opsdash_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly currentUser = signal<AuthResponse | null>(null);
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly isAdmin = computed(() => this.currentUser()?.role === 'Admin');

  constructor() {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) {
      return;
    }

    try {
      const user = JSON.parse(raw) as AuthResponse;
      this.currentUser.set(user);
    } catch {
      localStorage.removeItem(USER_KEY);
      localStorage.removeItem(TOKEN_KEY);
    }
  }

  login(request: LoginRequest): Observable<ApiResponse<AuthResponse>> {
    return this.api.post<AuthResponse>('/auth/login', request).pipe(
      tap((res) => {
        if (res.success && res.data) {
          this.persistAuth(res.data);
        }
      }),
    );
  }

  register(request: RegisterRequest): Observable<ApiResponse<AuthResponse>> {
    return this.api.post<AuthResponse>('/auth/register', request).pipe(
      tap((res) => {
        if (res.success && res.data) {
          this.persistAuth(res.data);
        }
      }),
    );
  }

  logout(): void {
    const refresh = this.currentUser()?.refreshToken;
    if (refresh) {
      this.api.post<boolean>('/auth/revoke', { refreshToken: refresh }).subscribe({
        complete: () => this.clearSession(),
        error: () => this.clearSession(),
      });
    } else {
      this.clearSession();
    }
  }

  refreshToken(): Observable<ApiResponse<AuthResponse>> {
    const user = this.currentUser();
    if (!user) {
      return of({
        success: false,
        message: 'Not authenticated',
        data: null,
        errors: null,
      });
    }

    return this.api
      .post<AuthResponse>('/auth/refresh', {
        token: user.token,
        refreshToken: user.refreshToken,
      })
      .pipe(
        tap((res) => {
          if (res.success && res.data) {
            this.persistAuth(res.data);
          }
        }),
      );
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  getUserId(): number {
    return this.currentUser()?.userId ?? 0;
  }

  getTenantId(): number {
    return this.currentUser()?.tenantId ?? 0;
  }

  private persistAuth(data: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, data.token);
    localStorage.setItem(USER_KEY, JSON.stringify(data));
    this.currentUser.set(data);
  }

  private clearSession(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.currentUser.set(null);
    void this.router.navigate(['/login']);
  }
}
