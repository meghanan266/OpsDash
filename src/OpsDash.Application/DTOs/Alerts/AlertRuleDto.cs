namespace OpsDash.Application.DTOs.Alerts;

public class AlertRuleDto
{
    public int Id { get; set; }

    public string MetricName { get; set; } = string.Empty;

    public decimal Threshold { get; set; }

    public string Operator { get; set; } = string.Empty;

    public string AlertMode { get; set; } = string.Empty;

    public int? ForecastHorizon { get; set; }

    public bool IsActive { get; set; }

    public int CreatedBy { get; set; }

    public string CreatedByName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
