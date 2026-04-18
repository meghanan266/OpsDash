using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class MetricBaseline : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string MetricName { get; set; } = string.Empty;

    public decimal Mean { get; set; }

    public decimal StandardDeviation { get; set; }

    public string TrendDirection { get; set; } = string.Empty;

    public int DataPointCount { get; set; }

    public DateTime LastCalculatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
