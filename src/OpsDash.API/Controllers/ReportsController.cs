using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Reports;
using OpsDash.Application.Interfaces;

namespace OpsDash.API.Controllers;

[ApiController]
[Authorize]
[Tags("Reports")]
[Route("api/v1/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    [HttpPost("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<ReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ReportDto>>> GenerateDashboard(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var result = await _reports.GenerateDashboardReportAsync(startDate, endDate);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("incident/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ReportDto>>> GenerateIncident(int id)
    {
        var result = await _reports.GenerateIncidentReportAsync(id);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ReportDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ReportDto>>>> List([FromQuery] PagedRequest paging)
    {
        var result = await _reports.GetReportsAsync(paging);
        return Ok(result);
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var result = await _reports.DownloadReportAsync(id);
        if (!result.Success || result.Data is null)
        {
            return BadRequest(result);
        }

        var fileName = $"opsdash-report-{id}.csv";
        return File(result.Data, "text/csv", fileName);
    }
}
