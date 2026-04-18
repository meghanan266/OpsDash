namespace OpsDash.Application.DTOs.Metrics;

public class IngestMetricRequest
{
    public string MetricName { get; set; } = string.Empty;

    public decimal MetricValue { get; set; }

    public string Category { get; set; } = string.Empty;

    public DateTime? RecordedAt { get; set; }
}

