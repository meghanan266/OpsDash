namespace OpsDash.Application.DTOs.Anomalies;

public class AnomalyDto
{
    public long Id { get; set; }

    public string MetricName { get; set; } = string.Empty;

    public decimal MetricValue { get; set; }

    public decimal ZScore { get; set; }

    public string Severity { get; set; } = string.Empty;

    public DateTime DetectedAt { get; set; }

    public bool IsActive { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public int? IncidentId { get; set; }
}
