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
    private readonly ILogger<AnomalyDetectionService> _logger;
    private readonly AnomalyDetectionSettings _settings;

    public AnomalyDetectionService(
        IAppDbContext db,
        ITenantContextService tenantContext,
        ILogger<AnomalyDetectionService> logger,
        IOptions<AnomalyDetectionSettings> options)
    {
        _db = db;
        _tenantContext = tenantContext;
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

        try
        {
            var totalCount = await _db.Metrics.AsNoTracking()
                .CountAsync(m => m.TenantId == tenantId && m.MetricName == metric.MetricName);

            if (totalCount < MinDataPointsForAnalysis)
            {
                return SkippedAnalysisResult(metric, "Insufficient total data points for analysis.");
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
                    return SkippedAnalysisResult(metric, "Insufficient prior data points to compute a baseline.");
                }

                baselineMean = priorPoints.Average();
                baselineStdDev = PopulationStandardDeviation(priorPoints, baselineMean);
            }

            if (baselineStdDev < MinStdDev)
            {
                return SkippedAnalysisResult(metric, "Baseline standard deviation is too small for a stable Z-score.");
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

                return new AnomalyDetectionResult
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
                };
            }

            return new AnomalyDetectionResult
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
            };
        }
        finally
        {
            await UpdateBaselineAsync(tenantId, metric.MetricName);
        }
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
