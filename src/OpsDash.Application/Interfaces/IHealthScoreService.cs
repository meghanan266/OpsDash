using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.HealthScores;

namespace OpsDash.Application.Interfaces;

public interface IHealthScoreService
{
    Task<ApiResponse<HealthScoreDto>> GetLatestAsync();

    Task<ApiResponse<List<HealthScoreDto>>> GetHistoryAsync(int take = 30);
}
