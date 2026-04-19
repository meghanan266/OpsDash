export interface ApiResponse<T> {
  success: boolean;
  message: string | null;
  data: T | null;
  errors: string[] | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface HealthScore {
  id: number;
  overallScore: number;
  normalMetricPct: number;
  activeAnomalies: number;
  trendScore: number;
  responseScore: number;
  calculatedAt: string;
}
