namespace OpsDash.Application.DTOs.Metrics;

public class MetricDto
{
    public long Id { get; set; }

    public string MetricName { get; set; } = string.Empty;

    public decimal MetricValue { get; set; }

    public string Category { get; set; } = string.Empty;

    public DateTime RecordedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool AnomalyDetected { get; set; }
}

