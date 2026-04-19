using OpsDash.Application.DTOs.Anomalies;

namespace OpsDash.Application.DTOs.Incidents;

public class IncidentDetailDto : IncidentDto
{
    public List<IncidentEventDto> Events { get; set; } = [];

    /// <summary>Correlations from anomalies linked to this incident.</summary>
    public List<MetricCorrelationDto> CorrelatedMetrics { get; set; } = [];
}
