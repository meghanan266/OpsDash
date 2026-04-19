using OpsDash.Application.DTOs.Anomalies;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Interfaces;

public interface IAnomalyDetectionService
{
    Task<AnomalyDetectionResult> AnalyzeMetricAsync(long metricId);

    Task UpdateBaselineAsync(int tenantId, string metricName);

    Task<MetricBaseline?> GetBaselineAsync(int tenantId, string metricName);

    Task CheckAndResolveAnomaliesAsync(int tenantId, string metricName);
}
