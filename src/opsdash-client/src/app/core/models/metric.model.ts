export interface Metric {
  id: number;
  metricName: string;
  metricValue: number;
  category: string;
  recordedAt: string;
  createdAt: string;
}

export interface MetricSummary {
  metricName: string;
  category: string;
  latestValue: number;
  minValue: number;
  maxValue: number;
  avgValue: number;
  dataPointCount: number;
  latestRecordedAt: string;
  trendDirection: string;
}

export interface MetricHistoryPoint {
  recordedAt: string;
  metricValue: number;
}
