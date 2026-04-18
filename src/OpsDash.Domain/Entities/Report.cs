using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class Report : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string ReportType { get; set; } = string.Empty;

    public int GeneratedBy { get; set; }

    public string? BlobUrl { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public User GeneratedByUser { get; set; } = null!;
}
