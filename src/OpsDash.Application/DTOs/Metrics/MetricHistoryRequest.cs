namespace OpsDash.Application.DTOs.Metrics;

public class MetricHistoryRequest
{
    public string MetricName { get; set; } = string.Empty;

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Granularity { get; set; } = "raw";
}

