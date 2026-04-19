export interface AlertRule {
  id: number;
  metricName: string;
  threshold: number;
  operator: string;
  alertMode: string;
  forecastHorizon: number | null;
  isActive: boolean;
  createdBy: number;
  createdByName: string;
  createdAt: string;
}

export interface Alert {
  id: number;
  metricName: string;
  metricValue: number;
  threshold: number;
  operator: string;
  isPredictive: boolean;
  forecastedValue: number | null;
  triggeredAt: string;
  acknowledgedBy: number | null;
  acknowledgedByName: string | null;
  acknowledgedAt: string | null;
}
