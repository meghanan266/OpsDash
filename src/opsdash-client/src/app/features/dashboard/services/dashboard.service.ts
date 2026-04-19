import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import type { ApiResponse, HealthScore, PagedResult } from '../../../core/models/common.model';
import type {
  Anomaly,
  ForecastPoint,
  IncidentRow,
  MetricHistoryPoint,
  MetricRow,
  MetricSummary,
} from '../models/dashboard.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly api = inject(ApiService);

  getSummary(startDate?: string, endDate?: string): Observable<ApiResponse<MetricSummary[]>> {
    return this.api.get<MetricSummary[]>('/metrics/summary', {
      startDate: startDate ?? undefined,
      endDate: endDate ?? undefined,
    });
  }

  getCategories(): Observable<ApiResponse<string[]>> {
    return this.api.get<string[]>('/metrics/categories');
  }

  getMetricHistory(
    name: string,
    startDate?: string,
    endDate?: string,
    granularity?: string,
  ): Observable<ApiResponse<MetricHistoryPoint[]>> {
    const encoded = encodeURIComponent(name);
    return this.api.get<MetricHistoryPoint[]>(`/metrics/${encoded}/history`, {
      startDate: startDate ?? undefined,
      endDate: endDate ?? undefined,
      granularity: granularity ?? 'raw',
    });
  }

  getMetricForecast(
    name: string,
    method?: string,
    horizon?: number,
  ): Observable<ApiResponse<ForecastPoint[]>> {
    const encoded = encodeURIComponent(name);
    return this.api.get<ForecastPoint[]>(`/metrics/${encoded}/forecast`, {
      method: method ?? undefined,
      horizon: horizon ?? undefined,
    });
  }

  getHealthScore(): Observable<ApiResponse<HealthScore | null>> {
    return this.api.get<HealthScore | null>('/health-score');
  }

  getHealthScoreHistory(take = 7): Observable<ApiResponse<HealthScore[]>> {
    return this.api.get<HealthScore[]>('/health-score/history', { take });
  }

  getActiveAnomalies(page = 1, pageSize = 50): Observable<ApiResponse<PagedResult<Anomaly>>> {
    return this.api.get<PagedResult<Anomaly>>('/anomalies/active', {
      page,
      pageSize,
      sortBy: 'detectedAt',
      sortDirection: 'desc',
    });
  }

  /** Historical anomalies for a metric (chart markers). */
  getAnomaliesForMetric(metricName: string, page = 1, pageSize = 200): Observable<ApiResponse<PagedResult<Anomaly>>> {
    return this.api.get<PagedResult<Anomaly>>('/anomalies', {
      page,
      pageSize,
      sortBy: 'detectedAt',
      sortDirection: 'desc',
      metricName,
    });
  }

  getRecentIncidents(): Observable<ApiResponse<PagedResult<IncidentRow>>> {
    return this.api.get<PagedResult<IncidentRow>>('/incidents', {
      page: 1,
      pageSize: 5,
      sortBy: 'startedAt',
      sortDirection: 'desc',
    });
  }

  getMetrics(category?: string, page = 1, pageSize = 25, sortBy = 'recordedAt', sortDirection = 'desc'): Observable<ApiResponse<PagedResult<MetricRow>>> {
    return this.api.get<PagedResult<MetricRow>>('/metrics', {
      category: category ?? undefined,
      page,
      pageSize,
      sortBy,
      sortDirection,
    });
  }
}
