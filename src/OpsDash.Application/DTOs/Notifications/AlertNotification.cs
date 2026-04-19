namespace OpsDash.Application.DTOs.Notifications;

public sealed class AlertNotification
{
    public long AlertId { get; init; }

    public string MetricName { get; init; } = string.Empty;

    public decimal MetricValue { get; init; }

    public decimal Threshold { get; init; }

    public string Operator { get; init; } = string.Empty;

    public bool IsPredictive { get; init; }

    public DateTime TriggeredAt { get; init; }
}
