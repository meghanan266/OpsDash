namespace OpsDash.Application.DTOs.Alerts;

public class AlertDto
{
    public long Id { get; set; }

    public string MetricName { get; set; } = string.Empty;

    public decimal MetricValue { get; set; }

    public decimal Threshold { get; set; }

    public string Operator { get; set; } = string.Empty;

    public bool IsPredictive { get; set; }

    public decimal? ForecastedValue { get; set; }

    public DateTime TriggeredAt { get; set; }

    public int? AcknowledgedBy { get; set; }

    public string? AcknowledgedByName { get; set; }

    public DateTime? AcknowledgedAt { get; set; }
}
