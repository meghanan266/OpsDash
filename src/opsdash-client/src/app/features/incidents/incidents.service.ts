import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import type { ApiResponse, PagedResult } from '../../core/models/common.model';
import type { IncidentRow } from '../dashboard/models/dashboard.models';
import type { IncidentDetail, IncidentStats } from './models/incident.models';

@Injectable({ providedIn: 'root' })
export class IncidentsService {
  private readonly api = inject(ApiService);

  getStats(): Observable<ApiResponse<IncidentStats>> {
    return this.api.get<IncidentStats>('/incidents/stats');
  }

  getIncidents(
    page = 1,
    pageSize = 20,
    sortBy = 'startedAt',
    sortDirection: 'asc' | 'desc' = 'desc',
    status?: string | null,
    severity?: string | null,
  ): Observable<ApiResponse<PagedResult<IncidentRow>>> {
    return this.api.get<PagedResult<IncidentRow>>('/incidents', {
      page,
      pageSize,
      sortBy,
      sortDirection,
      status: status ?? undefined,
      severity: severity ?? undefined,
    });
  }

  getById(id: number): Observable<ApiResponse<IncidentDetail>> {
    return this.api.get<IncidentDetail>(`/incidents/${id}`);
  }

  acknowledge(id: number): Observable<ApiResponse<IncidentRow>> {
    return this.api.put<IncidentRow>(`/incidents/${id}/acknowledge`, {});
  }

  updateStatus(id: number, status: string): Observable<ApiResponse<IncidentRow>> {
    return this.api.put<IncidentRow>(`/incidents/${id}/status`, { status });
  }
}
