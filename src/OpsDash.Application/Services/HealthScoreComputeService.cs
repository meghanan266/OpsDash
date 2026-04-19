using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsDash.Application.DTOs.Notifications;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public sealed class HealthScoreComputeService : IHealthScoreComputeService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContextService _tenantContext;
    private readonly IRealtimeNotificationService _realtimeNotifications;
    private readonly ILogger<HealthScoreComputeService> _logger;

    public HealthScoreComputeService(
        IAppDbContext db,
        ITenantContextService tenantContext,
        IRealtimeNotificationService realtimeNotifications,
        ILogger<HealthScoreComputeService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _realtimeNotifications = realtimeNotifications;
        _logger = logger;
    }

    public async Task<decimal> ComputeAndStoreHealthScoreAsync()
    {
        var tenantId = _tenantContext.TenantId;
        var now = DateTime.UtcNow;

        var metricNames = await _db.Metrics.AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .Select(m => m.MetricName)
            .Distinct()
            .ToListAsync();

        var totalCount = metricNames.Count;
        decimal normalMetricPct;
        if (totalCount == 0)
        {
            normalMetricPct = 100m;
        }
        else
        {
            var activeMetricNames = await _db.AnomalyScores.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.IsActive)
                .Select(a => a.MetricName)
                .Distinct()
                .ToListAsync();

            var abnormalSet = activeMetricNames.ToHashSet(StringComparer.Ordinal);
            var normalCount = metricNames.Count(m => !abnormalSet.Contains(m));
            normalMetricPct = (decimal)normalCount / totalCount * 100m;
        }

        var activeAnomalyCount = await _db.AnomalyScores.AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId && a.IsActive);

        var anomalyDensityScore = Math.Max(0m, 100m - activeAnomalyCount * 10m);

        var recentStart = now.AddHours(-24);
        var previousStart = now.AddHours(-48);
        var recent24h = await _db.AnomalyScores.AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId && a.DetectedAt >= recentStart);

        var previous24h = await _db.AnomalyScores.AsNoTracking()
            .CountAsync(a =>
                a.TenantId == tenantId
                && a.DetectedAt >= previousStart
                && a.DetectedAt < recentStart);

        decimal trendScore;
        if (recent24h < previous24h)
        {
            var ratio = previous24h == 0
                ? 1m
                : Math.Min(1m, (decimal)(previous24h - recent24h) / previous24h);
            trendScore = Math.Clamp(80m + 20m * ratio, 80m, 100m);
        }
        else if (recent24h == previous24h)
        {
            trendScore = 50m;
        }
        else
        {
            var ratio = recent24h == 0
                ? 0m
                : Math.Min(1m, (decimal)(recent24h - previous24h) / recent24h);
            trendScore = Math.Clamp(40m - 20m * ratio, 20m, 40m);
        }

        var sevenDaysAgo = now.AddDays(-7);
        var acknowledgedAlerts = await _db.Alerts.AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId
                && a.TriggeredAt >= sevenDaysAgo
                && a.AcknowledgedAt != null)
            .Select(a => new { a.TriggeredAt, a.AcknowledgedAt })
            .ToListAsync();

        decimal responseScore;
        if (acknowledgedAlerts.Count == 0)
        {
            responseScore = 70m;
        }
        else
        {
            var totalMinutes = acknowledgedAlerts.Sum(a => (a.AcknowledgedAt!.Value - a.TriggeredAt).TotalMinutes);
            var avgMinutes = totalMinutes / acknowledgedAlerts.Count;
            responseScore = MapResponseScore(avgMinutes);
        }

        var overall = normalMetricPct * 0.40m
            + anomalyDensityScore * 0.30m
            + trendScore * 0.20m
            + responseScore * 0.10m;

        overall = Math.Clamp(overall, 0m, 100m);
        var roundedOverall = decimal.Round(overall, 2, MidpointRounding.AwayFromZero);

        _db.HealthScores.Add(new HealthScore
        {
            TenantId = tenantId,
            OverallScore = roundedOverall,
            NormalMetricPct = normalMetricPct,
            ActiveAnomalies = activeAnomalyCount,
            TrendScore = trendScore,
            ResponseScore = responseScore,
            CalculatedAt = now,
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("Health score computed for tenant {TenantId}: {Score}", tenantId, roundedOverall);

        try
        {
            await _realtimeNotifications.NotifyHealthScoreUpdatedAsync(
                tenantId,
                new HealthScoreNotification
                {
                    OverallScore = roundedOverall,
                    NormalMetricPct = normalMetricPct,
                    ActiveAnomalies = activeAnomalyCount,
                    CalculatedAt = now,
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push health score realtime notification for tenant {TenantId}", tenantId);
        }

        return roundedOverall;
    }

    private static decimal MapResponseScore(double avgMinutes)
    {
        if (avgMinutes < 15)
        {
            return 100m;
        }

        if (avgMinutes < 30)
        {
            return 80m;
        }

        if (avgMinutes < 60)
        {
            return 60m;
        }

        if (avgMinutes < 240)
        {
            return 40m;
        }

        return 20m;
    }
}
