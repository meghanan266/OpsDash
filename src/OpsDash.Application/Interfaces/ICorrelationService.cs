using OpsDash.Application.DTOs.Anomalies;

namespace OpsDash.Application.Interfaces;

public interface ICorrelationService
{
    Task<List<CorrelationResult>> FindCorrelationsAsync(long anomalyScoreId);
}
