using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Reports;

namespace OpsDash.Application.Interfaces;

public interface IReportService
{
    Task<ApiResponse<ReportDto>> GenerateDashboardReportAsync(DateTime? startDate, DateTime? endDate);

    Task<ApiResponse<ReportDto>> GenerateIncidentReportAsync(int incidentId);

    Task<ApiResponse<PagedResult<ReportDto>>> GetReportsAsync(PagedRequest paging);

    Task<ApiResponse<byte[]>> DownloadReportAsync(int reportId);
}
