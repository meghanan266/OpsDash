namespace OpsDash.Application.DTOs.Anomalies;

public sealed class CorrelationResult
{
    public long SourceAnomalyId { get; set; }

    public string CorrelatedMetricName { get; set; } = string.Empty;

    public decimal CorrelatedMetricValue { get; set; }

    public decimal CorrelatedZScore { get; set; }

    public int TimeOffsetSeconds { get; set; }

    public DateTime DetectedAt { get; set; }
}
