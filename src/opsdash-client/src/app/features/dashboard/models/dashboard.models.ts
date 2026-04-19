export interface MetricSummary {
  metricName: string;
  category: string;
  latestValue: number;
  minValue: number;
  maxValue: number;
  avgValue: number;
  dataPointCount: number;
  latestRecordedAt: string | null;
  trendDirection: string;
}

export interface MetricHistoryPoint {
  recordedAt: string;
  metricValue: number;
}

export interface MetricRow {
  id: number;
  metricName: string;
  metricValue: number;
  category: string;
  recordedAt: string;
  createdAt: string;
}

export interface Anomaly {
  id: number;
  metricName: string;
  metricValue: number;
  zScore: number;
  severity: string;
  detectedAt: string;
  isActive: boolean;
  resolvedAt: string | null;
  incidentId: number | null;
}

export interface IncidentRow {
  id: number;
  title: string;
  severity: string;
  status: string;
  anomalyCount: number;
  affectedMetrics: string;
  startedAt: string;
  acknowledgedAt: string | null;
  resolvedAt: string | null;
}

export interface ForecastPoint {
  metricName: string;
  forecastedValue: number;
  forecastMethod: string;
  forecastedFor: string;
  confidenceLower: number | null;
  confidenceUpper: number | null;
}
