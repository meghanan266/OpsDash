/** Higher values are generally better for the business. */
const POSITIVE_UP = new Set<string>([
  'active_users',
  'monthly_revenue',
  'daily_revenue',
  'avg_order_value',
  'customer_satisfaction',
  'patient_satisfaction',
  'checkout_conversion',
  'deployment_frequency',
  'patient_throughput',
  'payment_success_rate',
  'staffing_ratio',
]);

/** Higher values are generally worse. */
const NEGATIVE_UP = new Set<string>([
  'error_rate',
  'churn_rate',
  'api_response_time',
  'page_load_time',
  'er_wait_time',
  'support_tickets',
  'cart_abandonment',
  'readmission_rate',
  'appointment_no_shows',
  'avg_treatment_time',
  'order_fulfillment_time',
  'return_rate',
]);

export function isPositiveUpMetric(metricName: string): boolean {
  const key = metricName.toLowerCase();
  if (POSITIVE_UP.has(key)) {
    return true;
  }

  if (NEGATIVE_UP.has(key)) {
    return false;
  }

  if (
    key.includes('revenue') ||
    key.includes('conversion') ||
    key.includes('satisfaction') ||
    key.includes('throughput') ||
    key.includes('success_rate') ||
    key.includes('users') ||
    key.includes('frequency')
  ) {
    return true;
  }

  if (
    key.includes('error') ||
    key.includes('wait') ||
    key.includes('churn') ||
    key.includes('abandon') ||
    key.includes('load_time') ||
    key.includes('response_time') ||
    key.includes('no_show') ||
    key.includes('readmission') ||
    key.includes('return_rate') ||
    key.includes('fulfillment_time') ||
    key.includes('treatment_time')
  ) {
    return false;
  }

  return true;
}

export function formatMetricTitle(metricName: string): string {
  return metricName
    .split('_')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1).toLowerCase())
    .join(' ');
}

export function formatMetricValue(metricName: string, value: number): string {
  const key = metricName.toLowerCase();
  if (key.includes('revenue') || key === 'avg_order_value' || key === 'daily_revenue' || key === 'monthly_revenue') {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(value);
  }

  if (key.includes('satisfaction') || key === 'bed_occupancy') {
    return value.toLocaleString(undefined, { maximumFractionDigits: 2 });
  }

  if (key === 'active_users' || key.includes('tickets') || key.endsWith('_users')) {
    return Math.round(value).toLocaleString();
  }

  if (
    key.includes('rate') ||
    key.includes('conversion') ||
    key.includes('occupancy') ||
    key.includes('ratio') ||
    key.includes('churn') ||
    key.includes('abandonment') ||
    key.includes('no_show') ||
    key.includes('readmission') ||
    key === 'checkout_conversion'
  ) {
    return `${(value * 100).toFixed(2)}%`;
  }

  if (Number.isInteger(value) || Math.abs(value) >= 100) {
    return Math.round(value).toLocaleString();
  }

  return value.toLocaleString(undefined, { maximumFractionDigits: 2 });
}
