namespace OpsDash.Application.Interfaces;

public interface IIncidentAutoGroupService
{
    Task<int?> ProcessAnomalyForIncidentAsync(long anomalyScoreId);

    Task CheckAndAutoResolveIncidentsAsync();
}
