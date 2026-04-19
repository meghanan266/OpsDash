using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.HealthScores;
using OpsDash.Application.Interfaces;

namespace OpsDash.API.Controllers;

/// <summary>
/// Tenant health score snapshots for dashboard KPIs.
/// </summary>
[ApiController]
[Authorize]
[Tags("Health score")]
[Route("api/v1/health-score")]
public class HealthScoreController : ControllerBase
{
    private readonly IHealthScoreService _healthScoreService;

    public HealthScoreController(IHealthScoreService healthScoreService)
    {
        _healthScoreService = healthScoreService;
    }

    /// <summary>
    /// Returns the most recent health score for the current tenant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthScoreDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HealthScoreDto>>> GetLatest()
    {
        var result = await _healthScoreService.GetLatestAsync();
        return Ok(result);
    }

    /// <summary>
    /// Returns recent health score history (default last 30 records).
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<List<HealthScoreDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<HealthScoreDto>>>> GetHistory([FromQuery] int take = 30)
    {
        var result = await _healthScoreService.GetHistoryAsync(take);
        return Ok(result);
    }
}
