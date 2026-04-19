using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.HealthScores;
using OpsDash.Application.Interfaces;

namespace OpsDash.Application.Services;

public class HealthScoreService : IHealthScoreService
{
    private readonly IAppDbContext _db;
    private readonly IMapper _mapper;

    public HealthScoreService(IAppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<ApiResponse<HealthScoreDto>> GetLatestAsync()
    {
        var entity = await _db.HealthScores
            .OrderByDescending(h => h.CalculatedAt)
            .FirstOrDefaultAsync();

        if (entity is null)
        {
            return new ApiResponse<HealthScoreDto> { Success = true, Data = null };
        }

        return ApiResponse<HealthScoreDto>.Ok(_mapper.Map<HealthScoreDto>(entity));
    }

    public async Task<ApiResponse<List<HealthScoreDto>>> GetHistoryAsync(int take = 30)
    {
        var list = await _db.HealthScores
            .OrderByDescending(h => h.CalculatedAt)
            .Take(take)
            .ToListAsync();

        return ApiResponse<List<HealthScoreDto>>.Ok(_mapper.Map<List<HealthScoreDto>>(list));
    }
}
