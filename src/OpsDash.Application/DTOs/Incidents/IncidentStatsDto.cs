namespace OpsDash.Application.DTOs.Incidents;

public sealed class IncidentStatsDto
{
    public int OpenCount { get; set; }

    public int InvestigatingCount { get; set; }

    public int ResolvedLast24HoursCount { get; set; }
}
