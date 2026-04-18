using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class AuditLog : ITenantEntity
{
    public long Id { get; set; }

    public int TenantId { get; set; }

    public int UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public User User { get; set; } = null!;
}
