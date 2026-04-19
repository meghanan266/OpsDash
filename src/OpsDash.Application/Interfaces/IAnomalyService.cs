using OpsDash.Application.DTOs.Anomalies;
using OpsDash.Application.DTOs.Common;

namespace OpsDash.Application.Interfaces;

public interface IAnomalyService
{
    Task<ApiResponse<PagedResult<AnomalyDto>>> GetAnomaliesAsync(PagedRequest paging, string? metricName = null);

    Task<ApiResponse<PagedResult<AnomalyDto>>> GetActiveAnomaliesAsync(PagedRequest paging);

    Task<ApiResponse<AnomalyDetailDto>> GetByIdAsync(long id);
}
