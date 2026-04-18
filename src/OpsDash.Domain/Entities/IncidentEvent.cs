using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class IncidentEvent : ITenantEntity
{
    public long Id { get; set; }

    public int IncidentId { get; set; }

    public int TenantId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? MetricName { get; set; }

    public decimal? MetricValue { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Incident Incident { get; set; } = null!;

    public User? CreatedByUser { get; set; }
}
