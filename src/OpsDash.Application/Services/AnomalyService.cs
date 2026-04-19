using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OpsDash.Application.DTOs.Anomalies;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public class AnomalyService : IAnomalyService
{
    private readonly IAppDbContext _db;
    private readonly IMapper _mapper;

    public AnomalyService(IAppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<ApiResponse<PagedResult<AnomalyDto>>> GetAnomaliesAsync(PagedRequest paging)
    {
        paging ??= new PagedRequest();
        if (string.IsNullOrWhiteSpace(paging.SortBy))
        {
            paging.SortBy = "detectedat";
            paging.SortDirection = "desc";
        }

        var query = _db.AnomalyScores.AsNoTracking();
        query = ApplySorting(query, paging);

        var total = await query.CountAsync();
        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync();

        return ApiResponse<PagedResult<AnomalyDto>>.Ok(new PagedResult<AnomalyDto>
        {
            Items = _mapper.Map<List<AnomalyDto>>(items),
            TotalCount = total,
            Page = paging.Page,
            PageSize = paging.PageSize,
        });
    }

    public async Task<ApiResponse<PagedResult<AnomalyDto>>> GetActiveAnomaliesAsync(PagedRequest paging)
    {
        paging ??= new PagedRequest();
        if (string.IsNullOrWhiteSpace(paging.SortBy))
        {
            paging.SortBy = "detectedat";
            paging.SortDirection = "desc";
        }

        var query = _db.AnomalyScores.AsNoTracking().Where(a => a.IsActive);
        query = ApplySorting(query, paging);

        var total = await query.CountAsync();
        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync();

        return ApiResponse<PagedResult<AnomalyDto>>.Ok(new PagedResult<AnomalyDto>
        {
            Items = _mapper.Map<List<AnomalyDto>>(items),
            TotalCount = total,
            Page = paging.Page,
            PageSize = paging.PageSize,
        });
    }

    public async Task<ApiResponse<AnomalyDetailDto>> GetByIdAsync(long id)
    {
        var anomaly = await _db.AnomalyScores.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (anomaly is null)
        {
            return ApiResponse<AnomalyDetailDto>.Fail("Anomaly not found.");
        }

        var correlations = await _db.MetricCorrelations.AsNoTracking()
            .Where(c => c.SourceAnomalyId == id)
            .OrderBy(c => c.DetectedAt)
            .ToListAsync();

        var detail = _mapper.Map<AnomalyDetailDto>(anomaly);
        detail.Correlations = _mapper.Map<List<MetricCorrelationDto>>(correlations);
        return ApiResponse<AnomalyDetailDto>.Ok(detail);
    }

    private static IQueryable<AnomalyScore> ApplySorting(IQueryable<AnomalyScore> query, PagedRequest paging)
    {
        var key = string.IsNullOrWhiteSpace(paging.SortBy)
            ? "detectedat"
            : paging.SortBy.Trim().ToLowerInvariant();
        var desc = string.Equals(paging.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return key switch
        {
            "metricname" => desc ? query.OrderByDescending(a => a.MetricName) : query.OrderBy(a => a.MetricName),
            "severity" => desc ? query.OrderByDescending(a => a.Severity) : query.OrderBy(a => a.Severity),
            "zscore" => desc ? query.OrderByDescending(a => a.ZScore) : query.OrderBy(a => a.ZScore),
            _ => desc ? query.OrderByDescending(a => a.DetectedAt) : query.OrderBy(a => a.DetectedAt),
        };
    }
}
