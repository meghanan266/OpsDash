namespace OpsDash.Application.DTOs.Incidents;

public class IncidentEventDto
{
    public long Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? MetricName { get; set; }

    public decimal? MetricValue { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
}
