using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class MetricCorrelation : ITenantEntity
{
    public long Id { get; set; }

    public int TenantId { get; set; }

    public long SourceAnomalyId { get; set; }

    public string CorrelatedMetricName { get; set; } = string.Empty;

    public decimal CorrelatedMetricValue { get; set; }

    public decimal CorrelatedZScore { get; set; }

    public int TimeOffsetSeconds { get; set; }

    public DateTime DetectedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public AnomalyScore SourceAnomaly { get; set; } = null!;
}
