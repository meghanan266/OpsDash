namespace OpsDash.Application.DTOs.Incidents;

public class IncidentDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int AnomalyCount { get; set; }

    public string AffectedMetrics { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
