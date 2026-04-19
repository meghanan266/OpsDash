using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsDash.Application.DTOs.Anomalies;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Incidents;
using OpsDash.Application.DTOs.Notifications;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public class IncidentService : IIncidentService
{
    private readonly IAppDbContext _db;
    private readonly IMapper _mapper;
    private readonly ITenantContextService _tenantContext;
    private readonly IRealtimeNotificationService _realtimeNotifications;
    private readonly ILogger<IncidentService> _logger;

    public IncidentService(
        IAppDbContext db,
        IMapper mapper,
        ITenantContextService tenantContext,
        IRealtimeNotificationService realtimeNotifications,
        ILogger<IncidentService> logger)
    {
        _db = db;
        _mapper = mapper;
        _tenantContext = tenantContext;
        _realtimeNotifications = realtimeNotifications;
        _logger = logger;
    }

    public async Task<ApiResponse<PagedResult<IncidentDto>>> GetIncidentsAsync(
        PagedRequest paging,
        string? status = null,
        string? severity = null)
    {
        paging ??= new PagedRequest();
        var query = _db.Incidents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(i => i.Status == st);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            var sev = severity.Trim();
            query = query.Where(i => i.Severity == sev);
        }

        query = ApplySorting(query, paging);

        var total = await query.CountAsync();
        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync();

        return ApiResponse<PagedResult<IncidentDto>>.Ok(new PagedResult<IncidentDto>
        {
            Items = _mapper.Map<List<IncidentDto>>(items),
            TotalCount = total,
            Page = paging.Page,
            PageSize = paging.PageSize,
        });
    }

    public async Task<ApiResponse<IncidentStatsDto>> GetStatsAsync()
    {
        var now = DateTime.UtcNow;
        var since = now.AddHours(-24);

        var openCount = await _db.Incidents.CountAsync(i => i.Status == "Open");
        var investigatingCount = await _db.Incidents.CountAsync(i => i.Status == "Investigating");
        var resolvedLast24 = await _db.Incidents.CountAsync(i =>
            i.Status == "Resolved"
            && i.ResolvedAt != null
            && i.ResolvedAt >= since);

        return ApiResponse<IncidentStatsDto>.Ok(new IncidentStatsDto
        {
            OpenCount = openCount,
            InvestigatingCount = investigatingCount,
            ResolvedLast24HoursCount = resolvedLast24,
        });
    }

    public async Task<ApiResponse<IncidentDetailDto>> GetByIdAsync(int id)
    {
        var incident = await _db.Incidents
            .AsNoTracking()
            .Include(i => i.Events)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (incident is null)
        {
            return ApiResponse<IncidentDetailDto>.Fail("Incident not found.");
        }

        var detail = _mapper.Map<IncidentDetailDto>(incident);
        detail.Events = (incident.Events ?? [])
            .OrderBy(e => e.CreatedAt)
            .Select(e => _mapper.Map<IncidentEventDto>(e))
            .ToList();

        var anomalyIds = await _db.AnomalyScores.AsNoTracking()
            .Where(a => a.IncidentId == id)
            .Select(a => a.Id)
            .ToListAsync();

        if (anomalyIds.Count > 0)
        {
            var correlations = await _db.MetricCorrelations.AsNoTracking()
                .Where(c => anomalyIds.Contains(c.SourceAnomalyId))
                .OrderBy(c => c.CorrelatedMetricName)
                .ThenBy(c => c.TimeOffsetSeconds)
                .ToListAsync();
            detail.CorrelatedMetrics = _mapper.Map<List<MetricCorrelationDto>>(correlations);
        }
        else
        {
            detail.CorrelatedMetrics = [];
        }

        return ApiResponse<IncidentDetailDto>.Ok(detail);
    }

    public async Task<ApiResponse<IncidentDto>> AcknowledgeAsync(int id, int userId)
    {
        var tenantId = _tenantContext.TenantId;
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == id);
        if (incident is null)
        {
            return ApiResponse<IncidentDto>.Fail("Incident not found.");
        }

        incident.AcknowledgedBy = userId;
        incident.AcknowledgedAt = DateTime.UtcNow;
        if (string.Equals(incident.Status, "Open", StringComparison.OrdinalIgnoreCase))
        {
            incident.Status = "Acknowledged";
        }

        _db.IncidentEvents.Add(new IncidentEvent
        {
            IncidentId = incident.Id,
            TenantId = tenantId,
            EventType = "Acknowledged",
            Description = "Incident acknowledged",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
        });

        await _db.SaveChangesAsync();

        try
        {
            await _realtimeNotifications.NotifyIncidentUpdatedAsync(tenantId, ToIncidentNotification(incident));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push incident updated notification for tenant {TenantId}", tenantId);
        }

        return ApiResponse<IncidentDto>.Ok(_mapper.Map<IncidentDto>(incident));
    }

    public async Task<ApiResponse<IncidentDto>> UpdateStatusAsync(int id, string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return ApiResponse<IncidentDto>.Fail("Status is required.");
        }

        var tenantId = _tenantContext.TenantId;
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == id);
        if (incident is null)
        {
            return ApiResponse<IncidentDto>.Fail("Incident not found.");
        }

        var newStatus = status.Trim();
        incident.Status = newStatus;

        _db.IncidentEvents.Add(new IncidentEvent
        {
            IncidentId = incident.Id,
            TenantId = tenantId,
            EventType = "StatusChanged",
            Description = $"Status changed to {newStatus}",
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        try
        {
            await _realtimeNotifications.NotifyIncidentUpdatedAsync(tenantId, ToIncidentNotification(incident));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push incident updated notification for tenant {TenantId}", tenantId);
        }

        return ApiResponse<IncidentDto>.Ok(_mapper.Map<IncidentDto>(incident));
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

    private static IQueryable<Incident> ApplySorting(IQueryable<Incident> query, PagedRequest paging)
    {
        var key = string.IsNullOrWhiteSpace(paging.SortBy)
            ? "startedat"
            : paging.SortBy.Trim().ToLowerInvariant();
        var desc = string.Equals(paging.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return key switch
        {
            "title" => desc ? query.OrderByDescending(i => i.Title) : query.OrderBy(i => i.Title),
            "severity" => desc ? query.OrderByDescending(i => i.Severity) : query.OrderBy(i => i.Severity),
            "status" => desc ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            _ => desc ? query.OrderByDescending(i => i.StartedAt) : query.OrderBy(i => i.StartedAt),
        };
    }
}
