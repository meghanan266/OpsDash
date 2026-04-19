namespace OpsDash.Application.DTOs.Incidents;

public class IncidentDetailDto : IncidentDto
{
    public List<IncidentEventDto> Events { get; set; } = [];
}
