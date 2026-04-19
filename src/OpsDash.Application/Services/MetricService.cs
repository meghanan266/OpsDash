using System.Globalization;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly IAnomalyDetectionService _anomalyDetectionService;
    private readonly IPredictiveAlertService _predictiveAlertService;
    private readonly IHealthScoreComputeService _healthScoreComputeService;
    private readonly ICacheService _cache;
    private readonly IDashboardSummaryQuery _dashboardSummaryQuery;
    private readonly ILogger<MetricService> _logger;

    public MetricService(
        IAppDbContext db,
        IMapper mapper,
        ITenantContextService tenantContext,
        IAnomalyDetectionService anomalyDetectionService,
        IPredictiveAlertService predictiveAlertService,
        IHealthScoreComputeService healthScoreComputeService,
        ICacheService cache,
        IDashboardSummaryQuery dashboardSummaryQuery,
        ILogger<MetricService> logger)
    {
        _db = db;
        _mapper = mapper;
        _tenantContext = tenantContext;
        _anomalyDetectionService = anomalyDetectionService;
        _predictiveAlertService = predictiveAlertService;
        _healthScoreComputeService = healthScoreComputeService;
        _cache = cache;
        _dashboardSummaryQuery = dashboardSummaryQuery;
        _logger = logger;
    }

    public async Task<ApiResponse<MetricDto>> IngestMetricAsync(IngestMetricRequest request)
    {
        var metric = _mapper.Map<Metric>(request);
        metric.TenantId = _tenantContext.TenantId;
        metric.RecordedAt = request.RecordedAt ?? DateTime.UtcNow;
        metric.CreatedAt = DateTime.UtcNow;

        _db.Metrics.Add(metric);
        await _db.SaveChangesAsync();

        var anomalyDetected = false;
        try
        {
            var anomalyResult = await _anomalyDetectionService.AnalyzeMetricAsync(metric.Id);
            anomalyDetected = anomalyResult.IsAnomaly;
            if (anomalyResult.IsAnomaly)
            {
                _logger.LogInformation(
                    "Ingest anomaly: {MetricName} value {Value}, severity {Severity}, Z-score {ZScore}",
                    anomalyResult.MetricName,
                    anomalyResult.MetricValue,
                    anomalyResult.Severity,
                    anomalyResult.ZScore);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anomaly detection failed during ingest for metric {MetricId}", metric.Id);
        }

        try
        {
            await _predictiveAlertService.EvaluateAlertsAsync(metric.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Current-mode alert evaluation failed for metric {MetricId}", metric.Id);
        }

        try
        {
            await _predictiveAlertService.EvaluatePredictiveAlertsAsync(metric.MetricName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Predictive alert evaluation failed for metric {MetricName}", metric.MetricName);
        }

        try
        {
            await TryRecomputeHealthScoreThrottledAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health score recomputation failed after ingest for tenant {TenantId}", _tenantContext.TenantId);
        }

        var dto = _mapper.Map<MetricDto>(metric);
        dto.AnomalyDetected = anomalyDetected;
        await InvalidateTenantMetricCachesAsync(metric.MetricName).ConfigureAwait(false);
        return ApiResponse<MetricDto>.Ok(dto);
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

        var dtos = _mapper.Map<List<MetricDto>>(metrics);
        for (var i = 0; i < metrics.Count; i++)
        {
            try
            {
                var result = await _anomalyDetectionService.AnalyzeMetricAsync(metrics[i].Id);
                if (result.IsAnomaly)
                {
                    _logger.LogInformation(
                        "Batch ingest anomaly: {MetricName} value {Value}, severity {Severity}, Z-score {ZScore}",
                        result.MetricName,
                        result.MetricValue,
                        result.Severity,
                        result.ZScore);
                }

                dtos[i].AnomalyDetected = result.IsAnomaly;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Anomaly detection failed during batch ingest for metric {MetricId}", metrics[i].Id);
                dtos[i].AnomalyDetected = false;
            }

            try
            {
                await _predictiveAlertService.EvaluateAlertsAsync(metrics[i].Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Current-mode alert evaluation failed for metric {MetricId}", metrics[i].Id);
            }

            try
            {
                await _predictiveAlertService.EvaluatePredictiveAlertsAsync(metrics[i].MetricName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Predictive alert evaluation failed for metric {MetricName}", metrics[i].MetricName);
            }
        }

        try
        {
            await TryRecomputeHealthScoreThrottledAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health score recomputation failed after batch ingest for tenant {TenantId}", tenantId);
        }

        foreach (var name in metrics.Select(m => m.MetricName).Distinct(StringComparer.Ordinal))
        {
            await InvalidateTenantMetricCachesAsync(name).ConfigureAwait(false);
        }

        return ApiResponse<List<MetricDto>>.Ok(dtos);
    }

    private async Task TryRecomputeHealthScoreThrottledAsync()
    {
        var tenantId = _tenantContext.TenantId;
        var last = await _db.HealthScores.AsNoTracking()
            .Where(h => h.TenantId == tenantId)
            .OrderByDescending(h => h.CalculatedAt)
            .FirstOrDefaultAsync();

        if (last is not null && last.CalculatedAt > DateTime.UtcNow.AddMinutes(-1))
        {
            return;
        }

        await _healthScoreComputeService.ComputeAndStoreHealthScoreAsync();
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

    public async Task<CachedApiResponse<List<MetricSummaryDto>>> GetMetricsSummaryAsync(DateTime? startDate, DateTime? endDate)
    {
        var tenantId = _tenantContext.TenantId;
        var key = SummaryCacheKey(tenantId, startDate, endDate);
        var cached = await _cache.GetAsync<List<MetricSummaryDto>>(key).ConfigureAwait(false);
        if (cached.IsHit && cached.Value is not null)
        {
            return new CachedApiResponse<List<MetricSummaryDto>>
            {
                Response = ApiResponse<List<MetricSummaryDto>>.Ok(cached.Value),
                FromCache = true,
            };
        }

        var summaries = await _dashboardSummaryQuery
            .GetDashboardSummaryAsync(tenantId, startDate, endDate)
            .ConfigureAwait(false);

        await _cache
            .SetAsync(key, summaries, TimeSpan.FromMinutes(2))
            .ConfigureAwait(false);

        return new CachedApiResponse<List<MetricSummaryDto>>
        {
            Response = ApiResponse<List<MetricSummaryDto>>.Ok(summaries),
            FromCache = false,
        };
    }

    public async Task<ApiResponse<List<string>>> GetCategoriesAsync()
    {
        var tenantId = _tenantContext.TenantId;
        var key = CategoriesCacheKey(tenantId);
        var cached = await _cache.GetAsync<List<string>>(key).ConfigureAwait(false);
        if (cached.IsHit && cached.Value is not null)
        {
            return ApiResponse<List<string>>.Ok(cached.Value);
        }

        var categories = await _db.Metrics
            .Select(m => m.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync()
            .ConfigureAwait(false);

        await _cache.SetAsync(key, categories, TimeSpan.FromMinutes(10)).ConfigureAwait(false);

        return ApiResponse<List<string>>.Ok(categories);
    }

    public async Task<ApiResponse<List<MetricHistoryPointDto>>> GetMetricHistoryAsync(MetricHistoryRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        var granularity = string.IsNullOrWhiteSpace(request.Granularity)
            ? "raw"
            : request.Granularity.Trim().ToLowerInvariant();

        var historyKey = HistoryCacheKey(tenantId, request.MetricName, request.StartDate, request.EndDate, granularity);
        var cached = await _cache.GetAsync<List<MetricHistoryPointDto>>(historyKey).ConfigureAwait(false);
        if (cached.IsHit && cached.Value is not null)
        {
            return ApiResponse<List<MetricHistoryPointDto>>.Ok(cached.Value);
        }

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

        await _cache.SetAsync(historyKey, points, TimeSpan.FromMinutes(2)).ConfigureAwait(false);

        return ApiResponse<List<MetricHistoryPointDto>>.Ok(points);
    }

    private async Task InvalidateTenantMetricCachesAsync(string metricName)
    {
        var tenantId = _tenantContext.TenantId;
        try
        {
            await _cache.RemoveAsync(CategoriesCacheKey(tenantId)).ConfigureAwait(false);
            await _cache.RemoveAsync(HealthLatestCacheKey(tenantId)).ConfigureAwait(false);
            await _cache.RemoveByPrefixAsync($"dashboard:summary:{tenantId}:").ConfigureAwait(false);
            await _cache.RemoveByPrefixAsync($"history:{tenantId}:{metricName}:").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache invalidation failed for tenant {TenantId}", tenantId);
        }
    }

    private static string SummaryCacheKey(int tenantId, DateTime? start, DateTime? end) =>
        $"dashboard:summary:{tenantId}:{FormatDateKey(start)}:{FormatDateKey(end)}";

    private static string CategoriesCacheKey(int tenantId) => $"categories:{tenantId}";

    private static string HistoryCacheKey(
        int tenantId,
        string metricName,
        DateTime? start,
        DateTime? end,
        string granularity) =>
        $"history:{tenantId}:{metricName}:{FormatDateKey(start)}:{FormatDateKey(end)}:{granularity}";

    private static string HealthLatestCacheKey(int tenantId) => $"health:{tenantId}:latest";

    private static string FormatDateKey(DateTime? value) =>
        value.HasValue
            ? value.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)
            : "null";

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
