using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class AnomalyScore : ITenantEntity
{
    public long Id { get; set; }

    public int TenantId { get; set; }

    public long MetricId { get; set; }

    public string MetricName { get; set; } = string.Empty;

    public decimal MetricValue { get; set; }

    public decimal ZScore { get; set; }

    public string Severity { get; set; } = string.Empty;

    public decimal BaselineMean { get; set; }

    public decimal BaselineStdDev { get; set; }

    public DateTime DetectedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? ResolvedAt { get; set; }

    public int? IncidentId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Metric Metric { get; set; } = null!;

    public Incident? Incident { get; set; }
}
