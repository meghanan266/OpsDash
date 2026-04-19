namespace OpsDash.Application.DTOs.Notifications;

public sealed class IncidentNotification
{
    public int IncidentId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public int AnomalyCount { get; init; }

    public string AffectedMetrics { get; init; } = string.Empty;

    public DateTime StartedAt { get; init; }
}
