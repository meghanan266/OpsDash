using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDash.API.Extensions;
using OpsDash.Application.DTOs.Alerts;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.Interfaces;

namespace OpsDash.API.Controllers;

/// <summary>
/// Tenant-scoped CRUD operations for metric alert rules.
/// </summary>
[ApiController]
[Authorize]
[Tags("Alert Rules")]
[Route("api/v1/alert-rules")]
public class AlertRulesController : ControllerBase
{
    private readonly IAlertService _alertService;

    public AlertRulesController(IAlertService alertService)
    {
        _alertService = alertService;
    }

    /// <summary>
    /// Returns a paginated list of alert rules for the current tenant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AlertRuleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AlertRuleDto>>>> GetAlertRules(
        [FromQuery] PagedRequest paging)
    {
        var result = await _alertService.GetAlertRulesAsync(paging);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new alert rule. The authenticated user is recorded as the creator.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AlertRuleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AlertRuleDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AlertRuleDto>>> Create([FromBody] CreateAlertRuleRequest request)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _alertService.CreateAlertRuleAsync(request, userId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Updates an existing alert rule.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AlertRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AlertRuleDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AlertRuleDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AlertRuleDto>>> Update(
        int id,
        [FromBody] UpdateAlertRuleRequest request)
    {
        var result = await _alertService.UpdateAlertRuleAsync(id, request);
        if (!result.Success)
        {
            if (string.Equals(result.Message, "Alert rule not found", StringComparison.Ordinal))
            {
                return NotFound(result);
            }

            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Permanently deletes an alert rule.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _alertService.DeleteAlertRuleAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}
