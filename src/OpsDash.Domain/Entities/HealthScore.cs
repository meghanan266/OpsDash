using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class HealthScore : ITenantEntity
{
    public long Id { get; set; }

    public int TenantId { get; set; }

    public decimal OverallScore { get; set; }

    public decimal NormalMetricPct { get; set; }

    public int ActiveAnomalies { get; set; }

    public decimal TrendScore { get; set; }

    public decimal ResponseScore { get; set; }

    public DateTime CalculatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
