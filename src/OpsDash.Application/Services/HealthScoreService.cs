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
    private readonly ICacheService _cache;
    private readonly ITenantContextService _tenantContext;

    public HealthScoreService(
        IAppDbContext db,
        IMapper mapper,
        ICacheService cache,
        ITenantContextService tenantContext)
    {
        _db = db;
        _mapper = mapper;
        _cache = cache;
        _tenantContext = tenantContext;
    }

    public async Task<CachedApiResponse<HealthScoreDto>> GetLatestAsync()
    {
        var tenantId = _tenantContext.TenantId;
        var key = $"health:{tenantId}:latest";
        var cached = await _cache.GetAsync<HealthScoreDto>(key).ConfigureAwait(false);
        if (cached.IsHit && cached.Value is not null)
        {
            return new CachedApiResponse<HealthScoreDto>
            {
                Response = ApiResponse<HealthScoreDto>.Ok(cached.Value),
                FromCache = true,
            };
        }

        var entity = await _db.HealthScores
            .OrderByDescending(h => h.CalculatedAt)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (entity is null)
        {
            return new CachedApiResponse<HealthScoreDto>
            {
                Response = new ApiResponse<HealthScoreDto> { Success = true, Data = null, Message = null },
                FromCache = false,
            };
        }

        var dto = _mapper.Map<HealthScoreDto>(entity);
        await _cache.SetAsync(key, dto, TimeSpan.FromMinutes(1)).ConfigureAwait(false);

        return new CachedApiResponse<HealthScoreDto>
        {
            Response = ApiResponse<HealthScoreDto>.Ok(dto),
            FromCache = false,
        };
    }

    public async Task<ApiResponse<List<HealthScoreDto>>> GetHistoryAsync(int take = 30)
    {
        var list = await _db.HealthScores
            .OrderByDescending(h => h.CalculatedAt)
            .Take(take)
            .ToListAsync()
            .ConfigureAwait(false);

        return ApiResponse<List<HealthScoreDto>>.Ok(_mapper.Map<List<HealthScoreDto>>(list));
    }
}
