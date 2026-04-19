using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsDash.Application.Configuration;
using OpsDash.Application.DTOs.Anomalies;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public sealed class CorrelationService : ICorrelationService
{
    private const decimal MinStdDev = 0.0001m;

    private readonly IAppDbContext _db;
    private readonly ITenantContextService _tenantContext;
    private readonly AnomalyDetectionSettings _settings;
    private readonly ILogger<CorrelationService> _logger;

    public CorrelationService(
        IAppDbContext db,
        ITenantContextService tenantContext,
        IOptions<AnomalyDetectionSettings> options,
        ILogger<CorrelationService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<List<CorrelationResult>> FindCorrelationsAsync(long anomalyScoreId)
    {
        var tenantId = _tenantContext.TenantId;

        var sourceAnomaly = await _db.AnomalyScores.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == anomalyScoreId && a.TenantId == tenantId);

        if (sourceAnomaly is null)
        {
            throw new InvalidOperationException($"Anomaly score {anomalyScoreId} was not found for the current tenant.");
        }

        var sourceMetric = await _db.Metrics.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == sourceAnomaly.MetricId && m.TenantId == tenantId);

        if (sourceMetric is null)
        {
            throw new InvalidOperationException(
                $"Source metric {sourceAnomaly.MetricId} for anomaly {anomalyScoreId} was not found.");
        }

        var windowMinutes = Math.Max(0, _settings.CorrelationWindowMinutes);
        var windowStart = sourceMetric.RecordedAt.AddMinutes(-windowMinutes);
        var windowEnd = sourceMetric.RecordedAt.AddMinutes(windowMinutes);
        var correlationZThreshold = _settings.CorrelationZScoreThreshold;

        var otherMetrics = await _db.Metrics.AsNoTracking()
            .Where(m =>
                m.TenantId == tenantId
                && m.MetricName != sourceAnomaly.MetricName
                && m.RecordedAt >= windowStart
                && m.RecordedAt <= windowEnd)
            .ToListAsync();

        var results = new List<CorrelationResult>();
        var detectedAt = DateTime.UtcNow;

        foreach (var group in otherMetrics.GroupBy(m => m.MetricName))
        {
            var metricName = group.Key;
            var baseline = await _db.MetricBaselines.AsNoTracking()
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.MetricName == metricName);

            if (baseline is null || baseline.StandardDeviation < MinStdDev)
            {
                continue;
            }

            Metric? bestPoint = null;
            decimal bestZ = 0;
            var bestAbsZ = 0d;

            foreach (var point in group)
            {
                var z = (point.MetricValue - baseline.Mean) / baseline.StandardDeviation;
                var absZ = Math.Abs((double)z);
                if (absZ < correlationZThreshold)
                {
                    continue;
                }

                if (absZ > bestAbsZ || bestPoint is null)
                {
                    bestAbsZ = absZ;
                    bestZ = z;
                    bestPoint = point;
                }
            }

            if (bestPoint is null)
            {
                continue;
            }

            var timeOffsetSeconds = (int)(bestPoint.RecordedAt - sourceMetric.RecordedAt).TotalSeconds;

            var entity = new MetricCorrelation
            {
                TenantId = tenantId,
                SourceAnomalyId = anomalyScoreId,
                CorrelatedMetricName = metricName,
                CorrelatedMetricValue = bestPoint.MetricValue,
                CorrelatedZScore = bestZ,
                TimeOffsetSeconds = timeOffsetSeconds,
                DetectedAt = detectedAt,
            };

            _db.MetricCorrelations.Add(entity);

            results.Add(new CorrelationResult
            {
                SourceAnomalyId = anomalyScoreId,
                CorrelatedMetricName = metricName,
                CorrelatedMetricValue = bestPoint.MetricValue,
                CorrelatedZScore = bestZ,
                TimeOffsetSeconds = timeOffsetSeconds,
                DetectedAt = detectedAt,
            });
        }

        if (results.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Found {Count} correlated metrics for anomaly on {MetricName}",
            results.Count,
            sourceAnomaly.MetricName);

        return results;
    }
}
