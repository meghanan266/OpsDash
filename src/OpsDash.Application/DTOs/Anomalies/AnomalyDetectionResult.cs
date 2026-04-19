namespace OpsDash.Application.DTOs.Anomalies;

public sealed class AnomalyDetectionResult
{
    public bool IsAnomaly { get; set; }

    public decimal ZScore { get; set; }

    public string? Severity { get; set; }

    public long MetricId { get; set; }

    public string MetricName { get; set; } = string.Empty;

    public decimal MetricValue { get; set; }

    public decimal BaselineMean { get; set; }

    public decimal BaselineStdDev { get; set; }

    public long? AnomalyScoreId { get; set; }
}
