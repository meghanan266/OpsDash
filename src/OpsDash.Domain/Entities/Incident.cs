using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class Incident : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Status { get; set; } = "Open";

    public int AnomalyCount { get; set; } = 1;

    public string AffectedMetrics { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public int? AcknowledgedBy { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public int? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public User? AcknowledgedByUser { get; set; }

    public User? ResolvedByUser { get; set; }

    public ICollection<AnomalyScore> Anomalies { get; set; } = new List<AnomalyScore>();

    public ICollection<IncidentEvent> Events { get; set; } = new List<IncidentEvent>();
}
