using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class MetricForecast : ITenantEntity
{
    public long Id { get; set; }

    public int TenantId { get; set; }

    public string MetricName { get; set; } = string.Empty;

    public decimal ForecastedValue { get; set; }

    public string ForecastMethod { get; set; } = string.Empty;

    public DateTime ForecastedFor { get; set; }

    public decimal? ConfidenceLower { get; set; }

    public decimal? ConfidenceUpper { get; set; }

    public DateTime CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
