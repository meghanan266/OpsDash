using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsDash.Application.Configuration;
using OpsDash.Application.DTOs.Notifications;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public sealed class IncidentAutoGroupService : IIncidentAutoGroupService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IAppDbContext _db;
    private readonly ITenantContextService _tenantContext;
    private readonly AnomalyDetectionSettings _settings;
    private readonly IRealtimeNotificationService _realtimeNotifications;
    private readonly ILogger<IncidentAutoGroupService> _logger;

    public IncidentAutoGroupService(
        IAppDbContext db,
        ITenantContextService tenantContext,
        IOptions<AnomalyDetectionSettings> options,
        IRealtimeNotificationService realtimeNotifications,
        ILogger<IncidentAutoGroupService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _settings = options.Value;
        _realtimeNotifications = realtimeNotifications;
        _logger = logger;
    }

    public async Task<int?> ProcessAnomalyForIncidentAsync(long anomalyScoreId)
    {
        var tenantId = _tenantContext.TenantId;
        var windowMinutes = Math.Max(1, _settings.IncidentGroupingWindowMinutes);

        var anomaly = await _db.AnomalyScores
            .FirstOrDefaultAsync(a => a.Id == anomalyScoreId && a.TenantId == tenantId);

        if (anomaly is null)
        {
            return null;
        }

        var windowStart = anomaly.DetectedAt.AddMinutes(-windowMinutes);
        var openStatuses = new[] { "Open", "Acknowledged", "Investigating" };

        var existingIncident = await _db.Incidents
            .Where(i =>
                i.TenantId == tenantId
                && openStatuses.Contains(i.Status)
                && i.StartedAt >= windowStart
                && i.StartedAt <= anomaly.DetectedAt)
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync();

        if (existingIncident is not null)
        {
            anomaly.IncidentId = existingIncident.Id;
            existingIncident.AnomalyCount += 1;
            existingIncident.Severity = MaxSeverity(existingIncident.Severity, anomaly.Severity);
            existingIncident.AffectedMetrics = MergeAffectedMetricsJson(existingIncident.AffectedMetrics, anomaly.MetricName);

            _db.IncidentEvents.Add(new IncidentEvent
            {
                IncidentId = existingIncident.Id,
                TenantId = tenantId,
                EventType = "AnomalyDetected",
                Description =
                    $"New anomaly detected: {anomaly.MetricName} = {anomaly.MetricValue} (Z-score: {anomaly.ZScore}, Severity: {anomaly.Severity})",
                MetricName = anomaly.MetricName,
                MetricValue = anomaly.MetricValue,
                CreatedAt = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync();

            _logger.LogInformation("Anomaly {Id} added to existing incident {IncidentId}", anomaly.Id, existingIncident.Id);

            try
            {
                await _realtimeNotifications.NotifyIncidentUpdatedAsync(tenantId, ToIncidentNotification(existingIncident));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push incident updated notification for tenant {TenantId}", tenantId);
            }

            return existingIncident.Id;
        }

        var title = $"{anomaly.Severity} {anomaly.MetricName} anomaly detected";
        var incident = new Incident
        {
            TenantId = tenantId,
            Title = title,
            Severity = anomaly.Severity,
            Status = "Open",
            AnomalyCount = 1,
            AffectedMetrics = JsonSerializer.Serialize(new[] { anomaly.MetricName }, JsonOptions),
            StartedAt = anomaly.DetectedAt,
        };

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();

        anomaly.IncidentId = incident.Id;

        _db.IncidentEvents.Add(new IncidentEvent
        {
            IncidentId = incident.Id,
            TenantId = tenantId,
            EventType = "AnomalyDetected",
            Description =
                $"Incident created: {anomaly.MetricName} = {anomaly.MetricValue} (Z-score: {anomaly.ZScore}, Severity: {anomaly.Severity})",
            MetricName = anomaly.MetricName,
            MetricValue = anomaly.MetricValue,
            CreatedAt = DateTime.UtcNow,
        });

        var correlations = await _db.MetricCorrelations.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.SourceAnomalyId == anomalyScoreId)
            .ToListAsync();

        foreach (var c in correlations)
        {
            _db.IncidentEvents.Add(new IncidentEvent
            {
                IncidentId = incident.Id,
                TenantId = tenantId,
                EventType = "CorrelationFound",
                Description =
                    $"Correlated metric movement: {c.CorrelatedMetricName} = {c.CorrelatedMetricValue} (Z-score: {c.CorrelatedZScore}, {c.TimeOffsetSeconds}s offset)",
                MetricName = c.CorrelatedMetricName,
                MetricValue = c.CorrelatedMetricValue,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("New incident {Id} created for anomaly on {MetricName}", incident.Id, anomaly.MetricName);

        try
        {
            await _realtimeNotifications.NotifyIncidentCreatedAsync(tenantId, ToIncidentNotification(incident));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push incident created notification for tenant {TenantId}", tenantId);
        }

        return incident.Id;
    }

    public async Task CheckAndAutoResolveIncidentsAsync()
    {
        var tenantId = _tenantContext.TenantId;
        var openStatuses = new[] { "Open", "Acknowledged", "Investigating" };

        var incidents = await _db.Incidents
            .Where(i => i.TenantId == tenantId && openStatuses.Contains(i.Status))
            .ToListAsync();

        var now = DateTime.UtcNow;
        var anyChange = false;
        var autoResolved = new List<Incident>();

        foreach (var incident in incidents)
        {
            var linked = await _db.AnomalyScores
                .Where(a => a.TenantId == tenantId && a.IncidentId == incident.Id)
                .ToListAsync();

            if (linked.Count == 0)
            {
                continue;
            }

            if (linked.Any(a => a.IsActive))
            {
                continue;
            }

            incident.Status = "Resolved";
            incident.ResolvedAt = now;

            _db.IncidentEvents.Add(new IncidentEvent
            {
                IncidentId = incident.Id,
                TenantId = tenantId,
                EventType = "Resolved",
                Description = "Incident auto-resolved: all related metrics returned to normal range",
                CreatedAt = now,
            });

            anyChange = true;
            autoResolved.Add(incident);
            _logger.LogInformation("Incident {Id} auto-resolved", incident.Id);
        }

        if (anyChange)
        {
            await _db.SaveChangesAsync();

            foreach (var inc in autoResolved)
            {
                try
                {
                    await _realtimeNotifications.NotifyIncidentUpdatedAsync(tenantId, ToIncidentNotification(inc));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to push incident updated notification for tenant {TenantId}", tenantId);
                }
            }
        }
    }

    private static IncidentNotification ToIncidentNotification(Incident incident) =>
        new()
        {
            IncidentId = incident.Id,
            Title = incident.Title,
            Severity = incident.Severity,
            Status = incident.Status,
            AnomalyCount = incident.AnomalyCount,
            AffectedMetrics = string.IsNullOrEmpty(incident.AffectedMetrics) ? "[]" : incident.AffectedMetrics,
            StartedAt = incident.StartedAt,
        };

    private static string MergeAffectedMetricsJson(string? existingJson, string metricName)
    {
        var list = ParseMetricsArray(existingJson);
        if (!list.Contains(metricName, StringComparer.Ordinal))
        {
            list.Add(metricName);
        }

        return JsonSerializer.Serialize(list, JsonOptions);
    }

    private static List<string> ParseMetricsArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            return parsed ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string MaxSeverity(string a, string b)
    {
        return SeverityRank(a) >= SeverityRank(b) ? a : b;
    }

    private static int SeverityRank(string severity)
    {
        if (severity.Equals("Severe", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (severity.Equals("Critical", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }
}
