using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class AlertRule : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string MetricName { get; set; } = string.Empty;

    public decimal Threshold { get; set; }

    public string Operator { get; set; } = string.Empty;

    public string AlertMode { get; set; } = "Current";

    public int? ForecastHorizon { get; set; }

    public bool IsActive { get; set; } = true;

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public User CreatedByUser { get; set; } = null!;
}
