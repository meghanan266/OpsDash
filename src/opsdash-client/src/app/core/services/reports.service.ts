import { Injectable, inject } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import { ApiService } from './api.service';
import type { ApiResponse, PagedResult } from '../models/common.model';

export interface ReportRow {
  id: number;
  reportType: string;
  generatedByName: string;
  status: string;
  downloadUrl: string | null;
  createdAt: string;
  completedAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly api = inject(ApiService);

  generateDashboard(startDate?: string, endDate?: string): Observable<ApiResponse<ReportRow>> {
    return this.api.postWithQuery<ReportRow>(
      '/reports/dashboard',
      {},
      {
        startDate: startDate || undefined,
        endDate: endDate || undefined,
      },
    );
  }

  generateIncident(incidentId: number): Observable<ApiResponse<ReportRow>> {
    return this.api.postWithQuery<ReportRow>(`/reports/incident/${incidentId}`, {}, {});
  }

  list(page = 1, pageSize = 20): Observable<ApiResponse<PagedResult<ReportRow>>> {
    return this.api.get<PagedResult<ReportRow>>('/reports', { page, pageSize });
  }

  download(reportId: number): Observable<Blob> {
    return this.api.getBlob(`/reports/${reportId}/download`);
  }

  /** Generate then download as CSV in one flow. */
  downloadDashboardCsv(startDate?: string, endDate?: string): Observable<Blob> {
    return this.generateDashboard(startDate, endDate).pipe(
      switchMap((res) => {
        if (!res.success || !res.data) {
          throw new Error(res.message ?? 'Report generation failed');
        }

        return this.download(res.data.id);
      }),
    );
  }

  downloadIncidentCsv(incidentId: number): Observable<Blob> {
    return this.generateIncident(incidentId).pipe(
      switchMap((res) => {
        if (!res.success || !res.data) {
          throw new Error(res.message ?? 'Report generation failed');
        }

        return this.download(res.data.id);
      }),
    );
  }
}
