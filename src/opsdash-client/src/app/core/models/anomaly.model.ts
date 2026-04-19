export interface Anomaly {
  id: number;
  metricName: string;
  metricValue: number;
  zScore: number;
  severity: string;
  baselineMean: number;
  baselineStdDev: number;
  detectedAt: string;
  isActive: boolean;
  resolvedAt: string | null;
  incidentId: number | null;
}
