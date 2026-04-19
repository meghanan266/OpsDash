import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import type { ApiResponse, PagedResult } from '../../core/models/common.model';
import type { AlertHistoryRow, AlertRuleRow } from './models/alert.models';

@Injectable({ providedIn: 'root' })
export class AlertsService {
  private readonly api = inject(ApiService);

  getAlertRules(page = 1, pageSize = 50): Observable<ApiResponse<PagedResult<AlertRuleRow>>> {
    return this.api.get<PagedResult<AlertRuleRow>>('/alert-rules', {
      page,
      pageSize,
      sortBy: 'createdAt',
      sortDirection: 'desc',
    });
  }

  getAlerts(
    page = 1,
    pageSize = 50,
    isPredictive?: boolean | null,
  ): Observable<ApiResponse<PagedResult<AlertHistoryRow>>> {
    return this.api.get<PagedResult<AlertHistoryRow>>('/alerts', {
      page,
      pageSize,
      sortBy: 'triggeredAt',
      sortDirection: 'desc',
      isPredictive: isPredictive === undefined || isPredictive === null ? undefined : isPredictive,
    });
  }
}
