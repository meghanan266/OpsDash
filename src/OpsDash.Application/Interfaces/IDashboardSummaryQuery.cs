using OpsDash.Application.DTOs.Metrics;

namespace OpsDash.Application.Interfaces;

public interface IDashboardSummaryQuery
{
    Task<List<MetricSummaryDto>> GetDashboardSummaryAsync(
        int tenantId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);
}
