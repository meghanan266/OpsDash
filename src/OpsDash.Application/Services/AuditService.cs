using Microsoft.EntityFrameworkCore;
using OpsDash.Application.DTOs.Audit;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public sealed class AuditService : IAuditService
{
    private readonly IAppDbContext _db;

    public AuditService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<PagedResult<AuditLogDto>>> GetAuditLogsAsync(
        PagedRequest paging,
        string? entityName,
        string? action,
        DateTime? startDate,
        DateTime? endDate,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        paging ??= new PagedRequest();

        IQueryable<AuditLog> query = _db.AuditLogs.AsNoTracking().Include(a => a.User);

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var n = entityName.Trim();
            query = query.Where(a => a.EntityName == n);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var a = action.Trim();
            query = query.Where(x => x.Action == a);
        }

        if (startDate.HasValue)
        {
            query = query.Where(x => x.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(x => x.Timestamp <= endDate.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }

        query = query.OrderByDescending(x => x.Timestamp);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(Map).ToList();

        return ApiResponse<PagedResult<AuditLogDto>>.Ok(new PagedResult<AuditLogDto>
        {
            Items = items,
            TotalCount = total,
            Page = paging.Page,
            PageSize = paging.PageSize,
        });
    }

    private static AuditLogDto Map(AuditLog a)
    {
        var userName = $"{a.User.FirstName} {a.User.LastName}".Trim();
        if (string.IsNullOrEmpty(userName))
        {
            userName = a.User.Email;
        }

        return new AuditLogDto
        {
            Id = a.Id,
            UserId = a.UserId,
            UserName = userName,
            Action = a.Action,
            EntityName = a.EntityName,
            EntityId = a.EntityId,
            OldValues = a.OldValues,
            NewValues = a.NewValues,
            Timestamp = a.Timestamp,
        };
    }
}
