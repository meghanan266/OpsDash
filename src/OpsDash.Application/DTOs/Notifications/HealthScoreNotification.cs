namespace OpsDash.Application.DTOs.Notifications;

public sealed class HealthScoreNotification
{
    public decimal OverallScore { get; init; }

    public decimal NormalMetricPct { get; init; }

    public int ActiveAnomalies { get; init; }

    public DateTime CalculatedAt { get; init; }
}
