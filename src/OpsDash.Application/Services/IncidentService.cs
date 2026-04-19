using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Incidents;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public class IncidentService : IIncidentService
{
    private readonly IAppDbContext _db;
    private readonly IMapper _mapper;

    public IncidentService(IAppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<ApiResponse<PagedResult<IncidentDto>>> GetIncidentsAsync(PagedRequest paging)
    {
        paging ??= new PagedRequest();
        var query = _db.Incidents.AsNoTracking();
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
        return ApiResponse<IncidentDetailDto>.Ok(detail);
    }

    public async Task<ApiResponse<IncidentDto>> AcknowledgeAsync(int id, int userId)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == id);
        if (incident is null)
        {
            return ApiResponse<IncidentDto>.Fail("Incident not found.");
        }

        incident.AcknowledgedBy = userId;
        incident.AcknowledgedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<IncidentDto>.Ok(_mapper.Map<IncidentDto>(incident));
    }

    public async Task<ApiResponse<IncidentDto>> UpdateStatusAsync(int id, string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return ApiResponse<IncidentDto>.Fail("Status is required.");
        }

        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == id);
        if (incident is null)
        {
            return ApiResponse<IncidentDto>.Fail("Incident not found.");
        }

        incident.Status = status.Trim();
        await _db.SaveChangesAsync();

        return ApiResponse<IncidentDto>.Ok(_mapper.Map<IncidentDto>(incident));
    }

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
