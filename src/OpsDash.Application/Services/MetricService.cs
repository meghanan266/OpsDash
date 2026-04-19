using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Metrics;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public class MetricService : IMetricService
{
    private readonly IAppDbContext _db;
    private readonly IMapper _mapper;
    private readonly ITenantContextService _tenantContext;

    public MetricService(IAppDbContext db, IMapper mapper, ITenantContextService tenantContext)
    {
        _db = db;
        _mapper = mapper;
        _tenantContext = tenantContext;
    }

    public async Task<ApiResponse<MetricDto>> IngestMetricAsync(IngestMetricRequest request)
    {
        var metric = _mapper.Map<Metric>(request);
        metric.TenantId = _tenantContext.TenantId;
        metric.RecordedAt = request.RecordedAt ?? DateTime.UtcNow;
        metric.CreatedAt = DateTime.UtcNow;

        _db.Metrics.Add(metric);
        await _db.SaveChangesAsync();

        return ApiResponse<MetricDto>.Ok(_mapper.Map<MetricDto>(metric));
    }

    public async Task<ApiResponse<List<MetricDto>>> IngestBatchAsync(BatchIngestMetricRequest request)
    {
        var now = DateTime.UtcNow;
        var tenantId = _tenantContext.TenantId;

        var metrics = request.Metrics.Select(m =>
        {
            var entity = _mapper.Map<Metric>(m);
            entity.TenantId = tenantId;
            entity.RecordedAt = m.RecordedAt ?? now;
            entity.CreatedAt = now;
            return entity;
        }).ToList();

        await _db.Metrics.AddRangeAsync(metrics);
        await _db.SaveChangesAsync();

        return ApiResponse<List<MetricDto>>.Ok(_mapper.Map<List<MetricDto>>(metrics));
    }

    public async Task<ApiResponse<PagedResult<MetricDto>>> GetMetricsAsync(string? category, PagedRequest paging)
    {
        paging ??= new PagedRequest();

        IQueryable<Metric> query = _db.Metrics;

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim();
            query = query.Where(m => m.Category == cat);
        }

        var totalCount = await query.CountAsync();

        query = ApplySorting(query, paging);

        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync();

        var paged = new PagedResult<MetricDto>
        {
            Items = _mapper.Map<List<MetricDto>>(items),
            TotalCount = totalCount,
            Page = paging.Page,
            PageSize = paging.PageSize,
        };

        return ApiResponse<PagedResult<MetricDto>>.Ok(paged);
    }

    public async Task<ApiResponse<List<MetricSummaryDto>>> GetMetricsSummaryAsync(DateTime? startDate, DateTime? endDate)
    {
        const string sql = "EXEC [dbo].[sp_GetDashboardSummary] @TenantId, @StartDate, @EndDate";

        var summaries = await _db.Database
            .SqlQueryRaw<MetricSummaryDto>(
                sql,
                new SqlParameter("@TenantId", _tenantContext.TenantId),
                new SqlParameter("@StartDate", startDate.HasValue ? startDate.Value : DBNull.Value),
                new SqlParameter("@EndDate", endDate.HasValue ? endDate.Value : DBNull.Value))
            .ToListAsync();

        return ApiResponse<List<MetricSummaryDto>>.Ok(summaries);
    }

    public async Task<ApiResponse<List<string>>> GetCategoriesAsync()
    {
        var categories = await _db.Metrics
            .Select(m => m.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return ApiResponse<List<string>>.Ok(categories);
    }

    public async Task<ApiResponse<List<MetricHistoryPointDto>>> GetMetricHistoryAsync(MetricHistoryRequest request)
    {
        var granularity = string.IsNullOrWhiteSpace(request.Granularity)
            ? "raw"
            : request.Granularity.Trim().ToLowerInvariant();

        IQueryable<Metric> query = _db.Metrics.Where(m => m.MetricName == request.MetricName);

        if (request.StartDate.HasValue)
        {
            query = query.Where(m => m.RecordedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(m => m.RecordedAt <= request.EndDate.Value);
        }

        List<MetricHistoryPointDto> points = granularity switch
        {
            "hourly" => await query
                .GroupBy(m => new
                {
                    m.RecordedAt.Year,
                    m.RecordedAt.Month,
                    m.RecordedAt.Day,
                    m.RecordedAt.Hour,
                })
                .Select(g => new MetricHistoryPointDto
                {
                    RecordedAt = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0, DateTimeKind.Utc),
                    MetricValue = g.Average(x => x.MetricValue),
                })
                .OrderBy(p => p.RecordedAt)
                .ToListAsync(),

            "daily" => await query
                .GroupBy(m => new
                {
                    m.RecordedAt.Year,
                    m.RecordedAt.Month,
                    m.RecordedAt.Day,
                })
                .Select(g => new MetricHistoryPointDto
                {
                    RecordedAt = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, 0, 0, 0, DateTimeKind.Utc),
                    MetricValue = g.Average(x => x.MetricValue),
                })
                .OrderBy(p => p.RecordedAt)
                .ToListAsync(),

            _ => await query
                .OrderBy(m => m.RecordedAt)
                .Select(m => new MetricHistoryPointDto
                {
                    RecordedAt = m.RecordedAt,
                    MetricValue = m.MetricValue,
                })
                .ToListAsync(),
        };

        return ApiResponse<List<MetricHistoryPointDto>>.Ok(points);
    }

    private static IQueryable<Metric> ApplySorting(IQueryable<Metric> query, PagedRequest paging)
    {
        var sortKey = string.IsNullOrWhiteSpace(paging.SortBy)
            ? "recordedat"
            : paging.SortBy.Trim().ToLowerInvariant();

        var desc = string.Equals(paging.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortKey switch
        {
            "metricname" => desc ? query.OrderByDescending(m => m.MetricName) : query.OrderBy(m => m.MetricName),
            "category" => desc ? query.OrderByDescending(m => m.Category) : query.OrderBy(m => m.Category),
            "metricvalue" => desc ? query.OrderByDescending(m => m.MetricValue) : query.OrderBy(m => m.MetricValue),
            "createdat" => desc ? query.OrderByDescending(m => m.CreatedAt) : query.OrderBy(m => m.CreatedAt),
            "recordedat" => desc ? query.OrderByDescending(m => m.RecordedAt) : query.OrderBy(m => m.RecordedAt),
            _ => desc ? query.OrderByDescending(m => m.RecordedAt) : query.OrderBy(m => m.RecordedAt),
        };
    }
}

