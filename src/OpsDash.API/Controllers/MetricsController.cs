using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Metrics;
using OpsDash.Application.Interfaces;

namespace OpsDash.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/metrics")]
public class MetricsController : ControllerBase
{
    private readonly IMetricService _metricService;

    public MetricsController(IMetricService metricService)
    {
        _metricService = metricService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MetricDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<MetricDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<MetricDto>>> Ingest([FromBody] IngestMetricRequest request)
    {
        var result = await _metricService.IngestMetricAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(ApiResponse<List<MetricDto>>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<List<MetricDto>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<List<MetricDto>>>> IngestBatch([FromBody] BatchIngestMetricRequest request)
    {
        var result = await _metricService.IngestBatchAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<MetricDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<MetricDto>>>> GetMetrics(
        [FromQuery] string? category,
        [FromQuery] PagedRequest paging)
    {
        var result = await _metricService.GetMetricsAsync(category, paging);
        return Ok(result);
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<List<MetricSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<MetricSummaryDto>>>> GetSummary(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var result = await _metricService.GetMetricsSummaryAsync(startDate, endDate);
        return Ok(result);
    }

    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetCategories()
    {
        var result = await _metricService.GetCategoriesAsync();
        return Ok(result);
    }

    [HttpGet("{name}/history")]
    [ProducesResponseType(typeof(ApiResponse<List<MetricHistoryPointDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<MetricHistoryPointDto>>>> GetHistory(
        [FromRoute] string name,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? granularity)
    {
        var request = new MetricHistoryRequest
        {
            MetricName = name,
            StartDate = startDate,
            EndDate = endDate,
            Granularity = granularity ?? "raw",
        };

        var result = await _metricService.GetMetricHistoryAsync(request);
        return Ok(result);
    }
}

