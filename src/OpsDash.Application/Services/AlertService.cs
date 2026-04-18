using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OpsDash.Application.DTOs.Alerts;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public class AlertService : IAlertService
{
    private readonly IAppDbContext _db;
    private readonly IMapper _mapper;
    private readonly ITenantContextService _tenantContext;

    public AlertService(IAppDbContext db, IMapper mapper, ITenantContextService tenantContext)
    {
        _db = db;
        _mapper = mapper;
        _tenantContext = tenantContext;
    }

    public async Task<ApiResponse<PagedResult<AlertRuleDto>>> GetAlertRulesAsync(PagedRequest paging)
    {
        paging ??= new PagedRequest();

        IQueryable<AlertRule> query = _db.AlertRules;

        var totalCount = await query.CountAsync();

        query = ApplyAlertRuleSorting(query, paging);

        var items = await query
            .Include(r => r.CreatedByUser)
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync();

        var paged = new PagedResult<AlertRuleDto>
        {
            Items = _mapper.Map<List<AlertRuleDto>>(items),
            TotalCount = totalCount,
            Page = paging.Page,
            PageSize = paging.PageSize,
        };

        return ApiResponse<PagedResult<AlertRuleDto>>.Ok(paged);
    }

    public async Task<ApiResponse<AlertRuleDto>> CreateAlertRuleAsync(CreateAlertRuleRequest request, int userId)
    {
        var rule = _mapper.Map<AlertRule>(request);
        rule.MetricName = request.MetricName.Trim();
        rule.TenantId = _tenantContext.TenantId;
        rule.CreatedBy = userId;
        rule.CreatedAt = DateTime.UtcNow;
        rule.IsActive = true;

        if (string.Equals(request.AlertMode, "Current", StringComparison.OrdinalIgnoreCase))
        {
            rule.ForecastHorizon = null;
        }

        _db.AlertRules.Add(rule);
        await _db.SaveChangesAsync();

        var created = await _db.AlertRules
            .Include(r => r.CreatedByUser)
            .FirstAsync(r => r.Id == rule.Id);

        return ApiResponse<AlertRuleDto>.Ok(_mapper.Map<AlertRuleDto>(created));
    }

    public async Task<ApiResponse<AlertRuleDto>> UpdateAlertRuleAsync(int id, UpdateAlertRuleRequest request)
    {
        var rule = await _db.AlertRules
            .Include(r => r.CreatedByUser)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rule is null)
        {
            return ApiResponse<AlertRuleDto>.Fail("Alert rule not found");
        }

        if (request.MetricName is not null)
        {
            rule.MetricName = request.MetricName.Trim();
        }

        if (request.Threshold is decimal threshold)
        {
            rule.Threshold = threshold;
        }

        if (request.Operator is not null)
        {
            rule.Operator = request.Operator;
        }

        if (request.IsActive is bool active)
        {
            rule.IsActive = active;
        }

        if (request.AlertMode is not null)
        {
            rule.AlertMode = request.AlertMode;
            if (string.Equals(request.AlertMode, "Current", StringComparison.OrdinalIgnoreCase))
            {
                rule.ForecastHorizon = null;
            }
        }

        if (request.ForecastHorizon is int horizon
            && string.Equals(rule.AlertMode, "Predictive", StringComparison.OrdinalIgnoreCase))
        {
            rule.ForecastHorizon = horizon;
        }

        if (string.Equals(rule.AlertMode, "Current", StringComparison.OrdinalIgnoreCase))
        {
            rule.ForecastHorizon = null;
        }

        await _db.SaveChangesAsync();

        var updated = await _db.AlertRules
            .Include(r => r.CreatedByUser)
            .FirstAsync(r => r.Id == id);

        return ApiResponse<AlertRuleDto>.Ok(_mapper.Map<AlertRuleDto>(updated));
    }

    public async Task<ApiResponse<bool>> DeleteAlertRuleAsync(int id)
    {
        var rule = await _db.AlertRules.FirstOrDefaultAsync(r => r.Id == id);
        if (rule is null)
        {
            return ApiResponse<bool>.Fail("Alert rule not found");
        }

        _db.AlertRules.Remove(rule);
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<PagedResult<AlertDto>>> GetAlertsAsync(PagedRequest paging)
    {
        paging ??= new PagedRequest();

        IQueryable<Alert> query = _db.Alerts;

        var totalCount = await query.CountAsync();

        query = ApplyAlertSorting(query, paging);

        var items = await query
            .Include(a => a.AlertRule)
            .Include(a => a.AcknowledgedByUser)
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync();

        var paged = new PagedResult<AlertDto>
        {
            Items = _mapper.Map<List<AlertDto>>(items),
            TotalCount = totalCount,
            Page = paging.Page,
            PageSize = paging.PageSize,
        };

        return ApiResponse<PagedResult<AlertDto>>.Ok(paged);
    }

    public async Task<ApiResponse<bool>> AcknowledgeAlertAsync(long id, int userId)
    {
        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.Id == id);
        if (alert is null)
        {
            return ApiResponse<bool>.Fail("Alert not found");
        }

        if (alert.AcknowledgedAt.HasValue)
        {
            return ApiResponse<bool>.Fail("Alert already acknowledged");
        }

        alert.AcknowledgedBy = userId;
        alert.AcknowledgedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true);
    }

    private static IQueryable<AlertRule> ApplyAlertRuleSorting(IQueryable<AlertRule> query, PagedRequest paging)
    {
        var isDefaultSort = string.IsNullOrWhiteSpace(paging.SortBy);
        var sortKey = isDefaultSort ? "createdat" : paging.SortBy!.Trim().ToLowerInvariant();

        var desc = isDefaultSort
            || string.Equals(paging.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortKey switch
        {
            "metricname" => desc ? query.OrderByDescending(r => r.MetricName) : query.OrderBy(r => r.MetricName),
            "threshold" => desc ? query.OrderByDescending(r => r.Threshold) : query.OrderBy(r => r.Threshold),
            "isactive" => desc ? query.OrderByDescending(r => r.IsActive) : query.OrderBy(r => r.IsActive),
            "createdat" => desc ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt),
            _ => desc ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt),
        };
    }

    private static IQueryable<Alert> ApplyAlertSorting(IQueryable<Alert> query, PagedRequest paging)
    {
        var isDefaultSort = string.IsNullOrWhiteSpace(paging.SortBy);
        var sortKey = isDefaultSort ? "triggeredat" : paging.SortBy!.Trim().ToLowerInvariant();

        var desc = isDefaultSort
            || string.Equals(paging.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortKey switch
        {
            "metricvalue" => desc ? query.OrderByDescending(a => a.MetricValue) : query.OrderBy(a => a.MetricValue),
            "triggeredat" => desc ? query.OrderByDescending(a => a.TriggeredAt) : query.OrderBy(a => a.TriggeredAt),
            _ => desc ? query.OrderByDescending(a => a.TriggeredAt) : query.OrderBy(a => a.TriggeredAt),
        };
    }
}
