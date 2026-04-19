namespace OpsDash.Application.DTOs.Reports;

public sealed class ReportDto
{
    public int Id { get; init; }

    public string ReportType { get; init; } = string.Empty;

    public string GeneratedByName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? DownloadUrl { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? CompletedAt { get; init; }
}
