import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import type { ApiResponse, PagedResult } from '../../core/models/common.model';
import type {
  AlertHistoryRow,
  AlertRuleRow,
  CreateAlertRuleRequest,
  UpdateAlertRuleRequest,
} from './models/alert.models';

@Injectable({ providedIn: 'root' })
export class AlertsService {
  private readonly api = inject(ApiService);

  getAlertRules(
    page = 1,
    pageSize = 20,
    sortBy = 'createdAt',
    sortDirection: 'asc' | 'desc' = 'desc',
  ): Observable<ApiResponse<PagedResult<AlertRuleRow>>> {
    return this.api.get<PagedResult<AlertRuleRow>>('/alert-rules', {
      page,
      pageSize,
      sortBy,
      sortDirection,
    });
  }

  createAlertRule(request: CreateAlertRuleRequest): Observable<ApiResponse<AlertRuleRow>> {
    return this.api.post<AlertRuleRow>('/alert-rules', request);
  }

  updateAlertRule(id: number, request: UpdateAlertRuleRequest): Observable<ApiResponse<AlertRuleRow>> {
    return this.api.put<AlertRuleRow>(`/alert-rules/${id}`, request);
  }

  deleteAlertRule(id: number): Observable<ApiResponse<boolean>> {
    return this.api.delete<boolean>(`/alert-rules/${id}`);
  }

  getAlerts(
    page = 1,
    pageSize = 20,
    isPredictive?: boolean | null,
    sortBy = 'triggeredAt',
    sortDirection: 'asc' | 'desc' = 'desc',
  ): Observable<ApiResponse<PagedResult<AlertHistoryRow>>> {
    return this.api.get<PagedResult<AlertHistoryRow>>('/alerts', {
      page,
      pageSize,
      sortBy,
      sortDirection,
      isPredictive: isPredictive === undefined || isPredictive === null ? undefined : isPredictive,
    });
  }

  acknowledgeAlert(id: number): Observable<ApiResponse<boolean>> {
    return this.api.put<boolean>(`/alerts/${id}/acknowledge`, {});
  }
}
