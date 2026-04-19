using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsDash.Application.DTOs.Notifications;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public sealed class PredictiveAlertService : IPredictiveAlertService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContextService _tenantContext;
    private readonly IForecastService _forecastService;
    private readonly IRealtimeNotificationService _realtimeNotifications;
    private readonly ILogger<PredictiveAlertService> _logger;

    public PredictiveAlertService(
        IAppDbContext db,
        ITenantContextService tenantContext,
        IForecastService forecastService,
        IRealtimeNotificationService realtimeNotifications,
        ILogger<PredictiveAlertService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _forecastService = forecastService;
        _realtimeNotifications = realtimeNotifications;
        _logger = logger;
    }

    public async Task EvaluateAlertsAsync(long metricId)
    {
        var tenantId = _tenantContext.TenantId;

        var metric = await _db.Metrics.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == metricId && m.TenantId == tenantId);

        if (metric is null)
        {
            return;
        }

        var rules = await _db.AlertRules.AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId
                && r.IsActive
                && r.MetricName == metric.MetricName
                && r.AlertMode == "Current")
            .ToListAsync();

        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);

        foreach (var rule in rules)
        {
            if (!IsCurrentConditionTriggered(rule.Operator, metric.MetricValue, rule.Threshold))
            {
                continue;
            }

            var recent = await _db.Alerts.AsNoTracking()
                .AnyAsync(a =>
                    a.TenantId == tenantId
                    && a.AlertRuleId == rule.Id
                    && !a.IsPredictive
                    && a.TriggeredAt >= oneHourAgo);

            if (recent)
            {
                continue;
            }

            var alert = new Alert
            {
                TenantId = tenantId,
                AlertRuleId = rule.Id,
                MetricValue = metric.MetricValue,
                IsPredictive = false,
                ForecastedValue = null,
                TriggeredAt = now,
            };

            _db.Alerts.Add(alert);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Alert triggered: {MetricName} {Operator} {Threshold}, actual value: {Value}",
                metric.MetricName,
                rule.Operator,
                rule.Threshold,
                metric.MetricValue);

            try
            {
                await _realtimeNotifications.NotifyAlertTriggeredAsync(
                    tenantId,
                    new AlertNotification
                    {
                        AlertId = alert.Id,
                        MetricName = metric.MetricName,
                        MetricValue = metric.MetricValue,
                        Threshold = rule.Threshold,
                        Operator = rule.Operator,
                        IsPredictive = false,
                        TriggeredAt = now,
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push alert realtime notification for tenant {TenantId}", tenantId);
            }
        }
    }

    public async Task EvaluatePredictiveAlertsAsync(string metricName)
    {
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return;
        }

        var tenantId = _tenantContext.TenantId;

        var rules = await _db.AlertRules.AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId
                && r.IsActive
                && r.MetricName == metricName
                && r.AlertMode == "Predictive")
            .ToListAsync();

        if (rules.Count == 0)
        {
            return;
        }

        var latestValue = await _db.Metrics.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.MetricName == metricName)
            .OrderByDescending(m => m.RecordedAt)
            .Select(m => m.MetricValue)
            .FirstOrDefaultAsync();

        var now = DateTime.UtcNow;
        var twentyFourHoursAgo = now.AddHours(-24);

        foreach (var rule in rules)
        {
            var horizon = rule.ForecastHorizon is > 0 ? rule.ForecastHorizon.Value : (int?)null;
            var forecast = await _forecastService.GenerateForecastAsync(metricName, method: null, horizon: horizon);

            if (forecast.Count == 0)
            {
                continue;
            }

            var take = Math.Min(forecast.Count, rule.ForecastHorizon ?? forecast.Count);
            var window = forecast.Take(take).ToList();

            decimal? breachValue = null;
            foreach (var point in window)
            {
                if (IsCurrentConditionTriggered(rule.Operator, point.ForecastedValue, rule.Threshold))
                {
                    breachValue = point.ForecastedValue;
                    break;
                }
            }

            if (breachValue is null)
            {
                continue;
            }

            var recentPredictive = await _db.Alerts.AsNoTracking()
                .AnyAsync(a =>
                    a.TenantId == tenantId
                    && a.AlertRuleId == rule.Id
                    && a.IsPredictive
                    && a.TriggeredAt >= twentyFourHoursAgo);

            if (recentPredictive)
            {
                continue;
            }

            var alert = new Alert
            {
                TenantId = tenantId,
                AlertRuleId = rule.Id,
                MetricValue = latestValue,
                IsPredictive = true,
                ForecastedValue = breachValue,
                TriggeredAt = now,
            };

            _db.Alerts.Add(alert);
            await _db.SaveChangesAsync();

            var horizonLabel = rule.ForecastHorizon ?? take;
            _logger.LogInformation(
                "Predictive alert: {MetricName} forecasted to breach {Operator} {Threshold} within {Horizon} periods",
                metricName,
                rule.Operator,
                rule.Threshold,
                horizonLabel);

            try
            {
                await _realtimeNotifications.NotifyAlertTriggeredAsync(
                    tenantId,
                    new AlertNotification
                    {
                        AlertId = alert.Id,
                        MetricName = metricName,
                        MetricValue = latestValue,
                        Threshold = rule.Threshold,
                        Operator = rule.Operator,
                        IsPredictive = true,
                        TriggeredAt = now,
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push predictive alert realtime notification for tenant {TenantId}", tenantId);
            }
        }
    }

    private static bool IsCurrentConditionTriggered(string op, decimal value, decimal threshold)
    {
        var o = op.Trim();
        if (o.Equals("GreaterThan", StringComparison.OrdinalIgnoreCase))
        {
            return value > threshold;
        }

        if (o.Equals("LessThan", StringComparison.OrdinalIgnoreCase))
        {
            return value < threshold;
        }

        if (o.Equals("Equals", StringComparison.OrdinalIgnoreCase))
        {
            return value == threshold;
        }

        return false;
    }
}
