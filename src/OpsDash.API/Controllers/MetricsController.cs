using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Metrics;
using OpsDash.Application.Interfaces;

namespace OpsDash.API.Controllers;

/// <summary>
/// Ingest and query operational metrics for the current tenant.
/// </summary>
[ApiController]
[Authorize]
[Tags("Metrics")]
[Route("api/v1/metrics")]
public class MetricsController : ControllerBase
{
    private readonly IMetricService _metricService;
    private readonly IForecastService _forecastService;
    private readonly ITenantContextService _tenantContext;

    public MetricsController(
        IMetricService metricService,
        IForecastService forecastService,
        ITenantContextService tenantContext)
    {
        _metricService = metricService;
        _forecastService = forecastService;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Records a single metric data point.
    /// </summary>
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

    /// <summary>
    /// Records up to 1000 metric data points in one request.
    /// </summary>
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

    /// <summary>
    /// Lists stored metrics with optional category filter and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<MetricDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<MetricDto>>>> GetMetrics(
        [FromQuery] string? category,
        [FromQuery] PagedRequest paging)
    {
        var result = await _metricService.GetMetricsAsync(category, paging);
        return Ok(result);
    }

    /// <summary>
    /// Returns per-metric aggregates and trend for dashboard use (via database procedure).
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<List<MetricSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<MetricSummaryDto>>>> GetSummary(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var wrapped = await _metricService.GetMetricsSummaryAsync(startDate, endDate);
        Response.Headers.Append("X-Cache", wrapped.FromCache ? "HIT" : "MISS");
        return Ok(wrapped.Response);
    }

    /// <summary>
    /// Lists distinct metric categories for the tenant.
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetCategories()
    {
        var result = await _metricService.GetCategoriesAsync();
        return Ok(result);
    }

    /// <summary>
    /// Returns time series for one metric with optional granularity (raw, hourly, daily).
    /// </summary>
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

    /// <summary>
    /// Generates a short-term forecast for the metric (on demand) and persists it for the tenant.
    /// </summary>
    [HttpGet("{name}/forecast")]
    [ProducesResponseType(typeof(ApiResponse<List<ForecastPointDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ForecastPointDto>>>> GetForecast(
        [FromRoute] string name,
        [FromQuery] string? method,
        [FromQuery] int? horizon)
    {
        var points = await _forecastService.GenerateForecastAsync(name, method, horizon);
        await _forecastService.StoreForecastAsync(_tenantContext.TenantId, name, points);
        return Ok(ApiResponse<List<ForecastPointDto>>.Ok(points));
    }
}
