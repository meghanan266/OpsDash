namespace OpsDash.Application.DTOs.Anomalies;

public class MetricCorrelationDto
{
    public long Id { get; set; }

    public string CorrelatedMetricName { get; set; } = string.Empty;

    public decimal CorrelatedMetricValue { get; set; }

    public decimal CorrelatedZScore { get; set; }

    public int TimeOffsetSeconds { get; set; }

    public DateTime DetectedAt { get; set; }
}
