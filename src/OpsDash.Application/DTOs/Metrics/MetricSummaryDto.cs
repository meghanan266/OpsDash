namespace OpsDash.Application.DTOs.Metrics;

public class MetricSummaryDto
{
    public string MetricName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal LatestValue { get; set; }

    public decimal MinValue { get; set; }

    public decimal MaxValue { get; set; }

    public decimal AvgValue { get; set; }

    public int DataPointCount { get; set; }

    public DateTime? LatestRecordedAt { get; set; }

    public string TrendDirection { get; set; } = "Stable";
}

