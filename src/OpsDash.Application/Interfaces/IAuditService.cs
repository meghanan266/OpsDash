using OpsDash.Application.DTOs.Audit;
using OpsDash.Application.DTOs.Common;

namespace OpsDash.Application.Interfaces;

public interface IAuditService
{
    Task<ApiResponse<PagedResult<AuditLogDto>>> GetAuditLogsAsync(
        PagedRequest paging,
        string? entityName,
        string? action,
        DateTime? startDate,
        DateTime? endDate,
        int? userId,
        CancellationToken cancellationToken = default);
}
