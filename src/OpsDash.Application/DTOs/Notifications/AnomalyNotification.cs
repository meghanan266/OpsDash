namespace OpsDash.Application.DTOs.Notifications;

public sealed class AnomalyNotification
{
    public long AnomalyId { get; init; }

    public string MetricName { get; init; } = string.Empty;

    public decimal MetricValue { get; init; }

    public decimal ZScore { get; init; }

    public string Severity { get; init; } = string.Empty;

    public DateTime DetectedAt { get; init; }

    public int? IncidentId { get; init; }
}
