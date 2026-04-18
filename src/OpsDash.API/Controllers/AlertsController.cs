using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDash.API.Extensions;
using OpsDash.Application.DTOs.Alerts;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.Interfaces;

namespace OpsDash.API.Controllers;

/// <summary>
/// Tenant-scoped operations for triggered alerts (read and acknowledge).
/// </summary>
[ApiController]
[Authorize]
[Tags("Alerts")]
[Route("api/v1/alerts")]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;

    public AlertsController(IAlertService alertService)
    {
        _alertService = alertService;
    }

    /// <summary>
    /// Returns a paginated list of triggered alerts for the current tenant, newest first by default.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AlertDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AlertDto>>>> GetAlerts([FromQuery] PagedRequest paging)
    {
        var result = await _alertService.GetAlertsAsync(paging);
        return Ok(result);
    }

    /// <summary>
    /// Marks an alert as acknowledged by the current user.
    /// </summary>
    [HttpPut("{id:long}/acknowledge")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<bool>>> Acknowledge(long id)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _alertService.AcknowledgeAlertAsync(id, userId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
