export interface AlertRuleRow {
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

export interface CreateAlertRuleRequest {
  metricName: string;
  operator: string;
  threshold: number;
  alertMode: string;
  forecastHorizon: number | null;
}

export interface UpdateAlertRuleRequest {
  metricName?: string;
  operator?: string;
  threshold?: number;
  alertMode?: string;
  forecastHorizon?: number | null;
  isActive?: boolean;
}

export interface AlertHistoryRow {
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
