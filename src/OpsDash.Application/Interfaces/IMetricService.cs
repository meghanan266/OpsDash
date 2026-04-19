using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Metrics;

namespace OpsDash.Application.Interfaces;

public interface IMetricService
{
    Task<ApiResponse<MetricDto>> IngestMetricAsync(IngestMetricRequest request);

    Task<ApiResponse<List<MetricDto>>> IngestBatchAsync(BatchIngestMetricRequest request);

    Task<ApiResponse<PagedResult<MetricDto>>> GetMetricsAsync(string? category, PagedRequest paging);

    Task<CachedApiResponse<List<MetricSummaryDto>>> GetMetricsSummaryAsync(DateTime? startDate, DateTime? endDate);

    Task<ApiResponse<List<string>>> GetCategoriesAsync();

    Task<ApiResponse<List<MetricHistoryPointDto>>> GetMetricHistoryAsync(MetricHistoryRequest request);
}

