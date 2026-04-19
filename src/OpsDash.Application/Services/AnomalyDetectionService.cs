using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsDash.Application.Configuration;
using OpsDash.Application.DTOs.Anomalies;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public sealed class AnomalyDetectionService : IAnomalyDetectionService
{
    private const int MinDataPointsForAnalysis = 5;
    private const decimal MinStdDev = 0.0001m;

    private readonly IAppDbContext _db;
    private readonly ITenantContextService _tenantContext;
    private readonly ICorrelationService _correlationService;
    private readonly IIncidentAutoGroupService _incidentAutoGroupService;
    private readonly ILogger<AnomalyDetectionService> _logger;
    private readonly AnomalyDetectionSettings _settings;

    public AnomalyDetectionService(
        IAppDbContext db,
        ITenantContextService tenantContext,
        ICorrelationService correlationService,
        IIncidentAutoGroupService incidentAutoGroupService,
        ILogger<AnomalyDetectionService> logger,
        IOptions<AnomalyDetectionSettings> options)
    {
        _db = db;
        _tenantContext = tenantContext;
        _correlationService = correlationService;
        _incidentAutoGroupService = incidentAutoGroupService;
        _logger = logger;
        _settings = options.Value;
    }

    public async Task<AnomalyDetectionResult> AnalyzeMetricAsync(long metricId)
    {
        var tenantId = _tenantContext.TenantId;

        var metric = await _db.Metrics.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == metricId && m.TenantId == tenantId);

        if (metric is null)
        {
            throw new InvalidOperationException($"Metric {metricId} was not found for the current tenant.");
        }

        AnomalyDetectionResult result;
        var shouldResolveAfterBaseline = false;

        try
        {
            var totalCount = await _db.Metrics.AsNoTracking()
                .CountAsync(m => m.TenantId == tenantId && m.MetricName == metric.MetricName);

            if (totalCount < MinDataPointsForAnalysis)
            {
                result = SkippedAnalysisResult(metric, "Insufficient total data points for analysis.");
                return result;
            }

            var persistedBaseline = await GetBaselineAsync(tenantId, metric.MetricName);

            decimal baselineMean;
            decimal baselineStdDev;

            if (persistedBaseline is not null)
            {
                baselineMean = persistedBaseline.Mean;
                baselineStdDev = persistedBaseline.StandardDeviation;
            }
            else
            {
                var priorPoints = await _db.Metrics.AsNoTracking()
                    .Where(m => m.TenantId == tenantId && m.MetricName == metric.MetricName && m.Id != metric.Id)
                    .OrderByDescending(m => m.RecordedAt)
                    .Take(_settings.BaselineDataPoints)
                    .Select(m => m.MetricValue)
                    .ToListAsync();

                if (priorPoints.Count < MinDataPointsForAnalysis)
                {
                    result = SkippedAnalysisResult(metric, "Insufficient prior data points to compute a baseline.");
                    return result;
                }

                baselineMean = priorPoints.Average();
                baselineStdDev = PopulationStandardDeviation(priorPoints, baselineMean);
            }

            if (baselineStdDev < MinStdDev)
            {
                result = SkippedAnalysisResult(metric, "Baseline standard deviation is too small for a stable Z-score.");
                return result;
            }

            var zScore = (metric.MetricValue - baselineMean) / baselineStdDev;
            var absZ = Math.Abs((double)zScore);

            var threshold = _settings.ZScoreThreshold;
            var isAnomaly = absZ >= threshold;

            string? severity = null;
            if (isAnomaly)
            {
                if (absZ >= _settings.SevereSeverityMin)
                {
                    severity = "Severe";
                }
                else if (absZ >= _settings.CriticalSeverityMin)
                {
                    severity = "Critical";
                }
                else if (absZ >= _settings.WarningSeverityMin)
                {
                    severity = "Warning";
                }
                else
                {
                    severity = "Warning";
                }

                var score = new AnomalyScore
                {
                    TenantId = tenantId,
                    MetricId = metric.Id,
                    MetricName = metric.MetricName,
                    MetricValue = metric.MetricValue,
                    ZScore = zScore,
                    Severity = severity,
                    BaselineMean = baselineMean,
                    BaselineStdDev = baselineStdDev,
                    DetectedAt = DateTime.UtcNow,
                    IsActive = true,
                };

                _db.AnomalyScores.Add(score);
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Anomaly detected: {MetricName} = {Value}, Z-score = {ZScore}, Severity = {Severity}",
                    metric.MetricName,
                    metric.MetricValue,
                    zScore,
                    severity);

                var correlations = await _correlationService.FindCorrelationsAsync(score.Id);

                int? incidentId = null;
                try
                {
                    incidentId = await _incidentAutoGroupService.ProcessAnomalyForIncidentAsync(score.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Incident grouping failed for anomaly score {AnomalyScoreId}", score.Id);
                }

                result = new AnomalyDetectionResult
                {
                    IsAnomaly = true,
                    ZScore = zScore,
                    Severity = severity,
                    MetricId = metric.Id,
                    MetricName = metric.MetricName,
                    MetricValue = metric.MetricValue,
                    BaselineMean = baselineMean,
                    BaselineStdDev = baselineStdDev,
                    AnomalyScoreId = score.Id,
                    Correlations = correlations,
                    IncidentId = incidentId,
                };

                return result;
            }

            shouldResolveAfterBaseline = true;
            result = new AnomalyDetectionResult
            {
                IsAnomaly = false,
                ZScore = zScore,
                Severity = null,
                MetricId = metric.Id,
                MetricName = metric.MetricName,
                MetricValue = metric.MetricValue,
                BaselineMean = baselineMean,
                BaselineStdDev = baselineStdDev,
                AnomalyScoreId = null,
                Correlations = [],
            };

            return result;
        }
        finally
        {
            await UpdateBaselineAsync(tenantId, metric.MetricName);

            if (shouldResolveAfterBaseline)
            {
                try
                {
                    await CheckAndResolveAnomaliesAsync(tenantId, metric.MetricName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Anomaly resolution check failed for metric {MetricName}", metric.MetricName);
                }
            }

            try
            {
                await _incidentAutoGroupService.CheckAndAutoResolveIncidentsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Incident auto-resolution check failed for tenant {TenantId}", tenantId);
            }
        }
    }

    public async Task CheckAndResolveAnomaliesAsync(int tenantId, string metricName)
    {
        EnsureTenant(tenantId);

        var baseline = await GetBaselineAsync(tenantId, metricName);
        if (baseline is null || baseline.StandardDeviation < MinStdDev)
        {
            return;
        }

        var latest = await _db.Metrics.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.MetricName == metricName)
            .OrderByDescending(m => m.RecordedAt)
            .FirstOrDefaultAsync();

        if (latest is null)
        {
            return;
        }

        var zScore = (latest.MetricValue - baseline.Mean) / baseline.StandardDeviation;
        if (Math.Abs((double)zScore) >= _settings.ZScoreThreshold)
        {
            return;
        }

        var active = await _db.AnomalyScores
            .Where(a => a.TenantId == tenantId && a.MetricName == metricName && a.IsActive)
            .ToListAsync();

        if (active.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var incidentIds = new HashSet<int>();

        foreach (var a in active)
        {
            a.IsActive = false;
            a.ResolvedAt = now;
            if (a.IncidentId.HasValue)
            {
                incidentIds.Add(a.IncidentId.Value);
            }
        }

        foreach (var incidentId in incidentIds)
        {
            _db.IncidentEvents.Add(new IncidentEvent
            {
                IncidentId = incidentId,
                TenantId = tenantId,
                EventType = "MetricNormalized",
                Description = $"{metricName} has returned to normal range",
                MetricName = metricName,
                CreatedAt = now,
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Anomaly resolved: {MetricName} returned to normal", metricName);
    }

    public async Task UpdateBaselineAsync(int tenantId, string metricName)
    {
        EnsureTenant(tenantId);

        var take = Math.Max(1, _settings.BaselineDataPoints);

        var points = await _db.Metrics.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.MetricName == metricName)
            .OrderByDescending(m => m.RecordedAt)
            .Take(take)
            .ToListAsync();

        if (points.Count == 0)
        {
            return;
        }

        var values = points.Select(p => p.MetricValue).ToList();
        var mean = values.Average();
        var stdDev = PopulationStandardDeviation(values, mean);
        var trend = ComputeTrendDirection(points);

        var existing = await _db.MetricBaselines
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.MetricName == metricName);

        if (existing is null)
        {
            _db.MetricBaselines.Add(new MetricBaseline
            {
                TenantId = tenantId,
                MetricName = metricName,
                Mean = mean,
                StandardDeviation = stdDev,
                TrendDirection = trend,
                DataPointCount = points.Count,
                LastCalculatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Mean = mean;
            existing.StandardDeviation = stdDev;
            existing.TrendDirection = trend;
            existing.DataPointCount = points.Count;
            existing.LastCalculatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public Task<MetricBaseline?> GetBaselineAsync(int tenantId, string metricName)
    {
        EnsureTenant(tenantId);

        return _db.MetricBaselines.AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.MetricName == metricName);
    }

    private void EnsureTenant(int tenantId)
    {
        if (tenantId != _tenantContext.TenantId)
        {
            throw new ArgumentException("The tenant id does not match the current tenant context.", nameof(tenantId));
        }
    }

    private AnomalyDetectionResult SkippedAnalysisResult(Metric metric, string reason)
    {
        _logger.LogDebug("Skipping anomaly analysis for metric {MetricId}: {Reason}", metric.Id, reason);
        return new AnomalyDetectionResult
        {
            IsAnomaly = false,
            ZScore = 0,
            Severity = null,
            MetricId = metric.Id,
            MetricName = metric.MetricName,
            MetricValue = metric.MetricValue,
            BaselineMean = 0,
            BaselineStdDev = 0,
            AnomalyScoreId = null,
            Correlations = [],
        };
    }

    private static decimal PopulationStandardDeviation(IReadOnlyList<decimal> values, decimal mean)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sumSquares = values.Sum(v =>
        {
            var d = v - mean;
            return d * d;
        });

        return (decimal)Math.Sqrt((double)(sumSquares / values.Count));
    }

    private static string ComputeTrendDirection(IReadOnlyList<Metric> orderedNewestFirst)
    {
        if (orderedNewestFirst.Count < 2)
        {
            return "Stable";
        }

        var half = orderedNewestFirst.Count / 2;
        if (half == 0)
        {
            return "Stable";
        }

        var recent = orderedNewestFirst.Take(half).Average(m => m.MetricValue);
        var older = orderedNewestFirst.Skip(half).Average(m => m.MetricValue);

        if (older == 0)
        {
            return recent > 0 ? "Rising" : "Stable";
        }

        if (recent > older * 1.02m)
        {
            return "Rising";
        }

        if (recent < older * 0.98m)
        {
            return "Falling";
        }

        return "Stable";
    }
}
