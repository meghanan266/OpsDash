using OpsDash.Application.DTOs.Metrics;

namespace OpsDash.Application.Interfaces;

public interface IForecastService
{
    Task<List<ForecastPointDto>> GenerateForecastAsync(string metricName, string? method = null, int? horizon = null);

    Task StoreForecastAsync(int tenantId, string metricName, List<ForecastPointDto> forecasts);
}
