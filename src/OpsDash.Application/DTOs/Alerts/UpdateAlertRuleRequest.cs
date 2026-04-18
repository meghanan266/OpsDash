namespace OpsDash.Application.DTOs.Alerts;

public class UpdateAlertRuleRequest
{
    public string? MetricName { get; set; }

    public decimal? Threshold { get; set; }

    public string? Operator { get; set; }

    public string? AlertMode { get; set; }

    public int? ForecastHorizon { get; set; }

    public bool? IsActive { get; set; }
}
