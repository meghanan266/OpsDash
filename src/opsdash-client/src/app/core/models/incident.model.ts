export interface Incident {
  id: number;
  title: string;
  severity: string;
  status: string;
  anomalyCount: number;
  affectedMetrics: string;
  startedAt: string;
  acknowledgedBy: number | null;
  acknowledgedAt: string | null;
  resolvedBy: number | null;
  resolvedAt: string | null;
}

export interface IncidentEvent {
  id: number;
  incidentId: number;
  eventType: string;
  description: string;
  metricName: string | null;
  metricValue: number | null;
  createdBy: number | null;
  createdAt: string;
}
