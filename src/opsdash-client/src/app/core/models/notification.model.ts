export interface AnomalyNotification {
  anomalyId: number;
  metricName: string;
  metricValue: number;
  zScore: number;
  severity: string;
  detectedAt: string;
  incidentId: number | null;
}

export interface IncidentNotification {
  incidentId: number;
  title: string;
  severity: string;
  status: string;
  anomalyCount: number;
  affectedMetrics: string;
  startedAt: string;
}

export interface HealthScoreNotification {
  overallScore: number;
  normalMetricPct: number;
  activeAnomalies: number;
  calculatedAt: string;
}

export interface AlertNotification {
  alertId: number;
  metricName: string;
  metricValue: number;
  threshold: number;
  operator: string;
  isPredictive: boolean;
  triggeredAt: string;
}

export type ToastTone = 'info' | 'warning' | 'danger' | 'success';

export interface AppToast {
  id: string;
  tone: ToastTone;
  title: string;
  description: string;
  createdAt: Date;
  /** Router navigate commands */
  route?: (string | number)[];
}
