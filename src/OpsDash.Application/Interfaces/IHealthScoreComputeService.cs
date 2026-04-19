namespace OpsDash.Application.Interfaces;

public interface IHealthScoreComputeService
{
    Task<decimal> ComputeAndStoreHealthScoreAsync();
}
