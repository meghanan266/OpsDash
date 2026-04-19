using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDash.Application.DTOs.Anomalies;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.Interfaces;

namespace OpsDash.API.Controllers;

/// <summary>
/// Detected anomalies for the current tenant.
/// </summary>
[ApiController]
[Authorize]
[Tags("Anomalies")]
[Route("api/v1/anomalies")]
public class AnomaliesController : ControllerBase
{
    private readonly IAnomalyService _anomalyService;

    public AnomaliesController(IAnomalyService anomalyService)
    {
        _anomalyService = anomalyService;
    }

    /// <summary>
    /// Returns a paginated list of anomalies.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AnomalyDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AnomalyDto>>>> GetAnomalies(
        [FromQuery] PagedRequest paging,
        [FromQuery] string? metricName = null)
    {
        var result = await _anomalyService.GetAnomaliesAsync(paging, metricName);
        return Ok(result);
    }

    /// <summary>
    /// Returns active anomalies only (not resolved).
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AnomalyDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AnomalyDto>>>> GetActive([FromQuery] PagedRequest paging)
    {
        var result = await _anomalyService.GetActiveAnomaliesAsync(paging);
        return Ok(result);
    }

    /// <summary>
    /// Returns a single anomaly with metric correlations.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AnomalyDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AnomalyDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AnomalyDetailDto>>> GetById(long id)
    {
        var result = await _anomalyService.GetByIdAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}
