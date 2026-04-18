using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class Alert : ITenantEntity
{
    public long Id { get; set; }

    public int TenantId { get; set; }

    public int AlertRuleId { get; set; }

    public decimal MetricValue { get; set; }

    public bool IsPredictive { get; set; } = false;

    public decimal? ForecastedValue { get; set; }

    public DateTime TriggeredAt { get; set; }

    public int? AcknowledgedBy { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public AlertRule AlertRule { get; set; } = null!;

    public User? AcknowledgedByUser { get; set; }
}
