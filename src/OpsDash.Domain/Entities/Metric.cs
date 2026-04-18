using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class Metric : ITenantEntity
{
    public long Id { get; set; }

    public int TenantId { get; set; }

    public string MetricName { get; set; } = string.Empty;

    public decimal MetricValue { get; set; }

    public string Category { get; set; } = string.Empty;

    public DateTime RecordedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
