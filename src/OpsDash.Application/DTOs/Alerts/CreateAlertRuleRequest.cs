namespace OpsDash.Application.DTOs.Alerts;

public class CreateAlertRuleRequest
{
    public string MetricName { get; set; } = string.Empty;

    public decimal Threshold { get; set; }

    public string Operator { get; set; } = string.Empty;

    public string AlertMode { get; set; } = "Current";

    public int? ForecastHorizon { get; set; }
}
