using OpsDash.Application.DTOs.Anomalies;
using OpsDash.Application.DTOs.Common;

namespace OpsDash.Application.Interfaces;

public interface IAnomalyService
{
    Task<ApiResponse<PagedResult<AnomalyDto>>> GetAnomaliesAsync(PagedRequest paging);

    Task<ApiResponse<PagedResult<AnomalyDto>>> GetActiveAnomaliesAsync(PagedRequest paging);

    Task<ApiResponse<AnomalyDetailDto>> GetByIdAsync(long id);
}
