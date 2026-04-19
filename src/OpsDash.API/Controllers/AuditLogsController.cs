using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDash.Application.DTOs.Audit;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.Interfaces;

namespace OpsDash.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Tags("Audit Logs")]
[Route("api/v1/audit-logs")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditLogsController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> Get(
        [FromQuery] PagedRequest paging,
        [FromQuery] string? entityName,
        [FromQuery] string? action,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int? userId,
        CancellationToken cancellationToken)
    {
        var result = await _auditService.GetAuditLogsAsync(
            paging,
            entityName,
            action,
            startDate,
            endDate,
            userId,
            cancellationToken);
        return Ok(result);
    }
}
