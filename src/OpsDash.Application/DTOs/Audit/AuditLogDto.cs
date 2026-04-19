namespace OpsDash.Application.DTOs.Audit;

public sealed class AuditLogDto
{
    public long Id { get; init; }

    public int? UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string EntityName { get; init; } = string.Empty;

    public string EntityId { get; init; } = string.Empty;

    public string? OldValues { get; init; }

    public string? NewValues { get; init; }

    public DateTime Timestamp { get; init; }
}
