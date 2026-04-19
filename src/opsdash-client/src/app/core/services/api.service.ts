import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { ApiResponse } from '../models/common.model';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl.replace(/\/$/, '');

  get<T>(path: string, params?: Record<string, string | number | boolean | null | undefined>): Observable<ApiResponse<T>> {
    let httpParams = new HttpParams();
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        if (value !== undefined && value !== null) {
          httpParams = httpParams.set(key, String(value));
        }
      }
    }

    return this.http.get<ApiResponse<T>>(this.join(path), { params: httpParams });
  }

  post<T>(path: string, body: unknown): Observable<ApiResponse<T>> {
    return this.http.post<ApiResponse<T>>(this.join(path), body);
  }

  postWithQuery<T>(
    path: string,
    body: unknown,
    params?: Record<string, string | number | boolean | null | undefined>,
  ): Observable<ApiResponse<T>> {
    let httpParams = new HttpParams();
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        if (value !== undefined && value !== null) {
          httpParams = httpParams.set(key, String(value));
        }
      }
    }

    return this.http.post<ApiResponse<T>>(this.join(path), body ?? {}, { params: httpParams });
  }

  getBlob(path: string): Observable<Blob> {
    return this.http.get(this.join(path), { responseType: 'blob' });
  }

  put<T>(path: string, body: unknown): Observable<ApiResponse<T>> {
    return this.http.put<ApiResponse<T>>(this.join(path), body);
  }

  delete<T>(path: string): Observable<ApiResponse<T>> {
    return this.http.delete<ApiResponse<T>>(this.join(path));
  }

  private join(path: string): string {
    const p = path.startsWith('/') ? path : `/${path}`;
    return `${this.baseUrl}${p}`;
  }
}
