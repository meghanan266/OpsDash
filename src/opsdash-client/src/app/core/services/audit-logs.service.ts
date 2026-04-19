import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import type { ApiResponse, PagedResult } from '../models/common.model';

export interface AuditLogRow {
  id: number;
  userId: number | null;
  userName: string;
  action: string;
  entityName: string;
  entityId: string;
  oldValues: string | null;
  newValues: string | null;
  timestamp: string;
}

@Injectable({ providedIn: 'root' })
export class AuditLogsService {
  private readonly api = inject(ApiService);

  list(params: {
    page: number;
    pageSize: number;
    entityName?: string;
    action?: string;
    startDate?: string;
    endDate?: string;
    userId?: number;
  }): Observable<ApiResponse<PagedResult<AuditLogRow>>> {
    return this.api.get<PagedResult<AuditLogRow>>('/audit-logs', {
      page: params.page,
      pageSize: params.pageSize,
      entityName: params.entityName,
      action: params.action,
      startDate: params.startDate,
      endDate: params.endDate,
      userId: params.userId,
    });
  }
}
