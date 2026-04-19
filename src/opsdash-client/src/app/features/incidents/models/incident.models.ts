import type { IncidentRow } from '../../dashboard/models/dashboard.models';

/** Matches API camelCase JSON. */
export interface IncidentStats {
  openCount: number;
  investigatingCount: number;
  resolvedLast24HoursCount: number;
}

export interface IncidentEventRow {
  id: number;
  eventType: string;
  description: string;
  metricName: string | null;
  metricValue: number | null;
  createdBy: number | null;
  createdAt: string;
}

export interface MetricCorrelationRow {
  id: number;
  correlatedMetricName: string;
  correlatedMetricValue: number;
  correlatedZScore: number;
  timeOffsetSeconds: number;
  detectedAt: string;
}

export interface IncidentDetail extends IncidentRow {
  events: IncidentEventRow[];
  correlatedMetrics: MetricCorrelationRow[];
}
