namespace OpsDash.Application.DTOs.Metrics;

public sealed class ForecastPointDto
{
    public string MetricName { get; set; } = string.Empty;

    public decimal ForecastedValue { get; set; }

    public string ForecastMethod { get; set; } = string.Empty;

    public DateTime ForecastedFor { get; set; }

    public decimal? ConfidenceLower { get; set; }

    public decimal? ConfidenceUpper { get; set; }
}
