using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDash.API.Extensions;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Incidents;
using OpsDash.Application.Interfaces;

namespace OpsDash.API.Controllers;

/// <summary>
/// Operational incidents for the current tenant.
/// </summary>
[ApiController]
[Authorize]
[Tags("Incidents")]
[Route("api/v1/incidents")]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;

    public IncidentsController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    /// <summary>
    /// Returns a paginated list of incidents.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<IncidentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<IncidentDto>>>> GetIncidents([FromQuery] PagedRequest paging)
    {
        var result = await _incidentService.GetIncidentsAsync(paging);
        return Ok(result);
    }

    /// <summary>
    /// Returns incident detail including timeline events.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<IncidentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IncidentDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IncidentDetailDto>>> GetById(int id)
    {
        var result = await _incidentService.GetByIdAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Acknowledges an incident on behalf of the current user.
    /// </summary>
    [HttpPut("{id:int}/acknowledge")]
    [ProducesResponseType(typeof(ApiResponse<IncidentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IncidentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IncidentDto>>> Acknowledge(int id)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _incidentService.AcknowledgeAsync(id, userId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Updates incident workflow status.
    /// </summary>
    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(ApiResponse<IncidentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IncidentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<IncidentDto>>> UpdateStatus(int id, [FromBody] UpdateIncidentStatusRequest request)
    {
        var result = await _incidentService.UpdateStatusAsync(id, request.Status);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
