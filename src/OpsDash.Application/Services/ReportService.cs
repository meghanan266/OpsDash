using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Metrics;
using OpsDash.Application.DTOs.Reports;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public sealed class ReportService : IReportService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContextService _tenantContext;
    private readonly IDashboardSummaryQuery _summaryQuery;
    private readonly ICurrentUserService _currentUser;

    public ReportService(
        IAppDbContext db,
        ITenantContextService tenantContext,
        IDashboardSummaryQuery summaryQuery,
        ICurrentUserService currentUser)
    {
        _db = db;
        _tenantContext = tenantContext;
        _summaryQuery = summaryQuery;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<ReportDto>> GenerateDashboardReportAsync(DateTime? startDate, DateTime? endDate)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return ApiResponse<ReportDto>.Fail("User context is required to generate a report.");
        }

        var tenantId = _tenantContext.TenantId;
        var report = new Report
        {
            TenantId = tenantId,
            ReportType = "Dashboard",
            GeneratedBy = userId.Value,
            Status = "Generating",
            CreatedAt = DateTime.UtcNow,
            FilterStartDate = startDate,
            FilterEndDate = endDate,
            SourceIncidentId = null,
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        try
        {
            var rows = await _summaryQuery
                .GetDashboardSummaryAsync(tenantId, startDate, endDate)
                .ConfigureAwait(false);
            var bytes = BuildDashboardCsv(rows);
            report.Status = "Completed";
            report.CompletedAt = DateTime.UtcNow;
            report.BlobUrl = null;
            await _db.SaveChangesAsync().ConfigureAwait(false);

            return ApiResponse<ReportDto>.Ok(await MapReportAsync(report.Id).ConfigureAwait(false));
        }
        catch (Exception)
        {
            report.Status = "Failed";
            report.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync().ConfigureAwait(false);
            return ApiResponse<ReportDto>.Fail("Report generation failed.");
        }
    }

    public async Task<ApiResponse<ReportDto>> GenerateIncidentReportAsync(int incidentId)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return ApiResponse<ReportDto>.Fail("User context is required to generate a report.");
        }

        var tenantId = _tenantContext.TenantId;
        var incident = await _db.Incidents
            .AsNoTracking()
            .Include(i => i.Events)
            .FirstOrDefaultAsync(i => i.Id == incidentId && i.TenantId == tenantId)
            .ConfigureAwait(false);

        if (incident is null)
        {
            return ApiResponse<ReportDto>.Fail("Incident not found.");
        }

        var anomalyIds = await _db.AnomalyScores.AsNoTracking()
            .Where(a => a.IncidentId == incidentId)
            .Select(a => a.Id)
            .ToListAsync()
            .ConfigureAwait(false);

        var correlations = anomalyIds.Count == 0
            ? new List<MetricCorrelation>()
            : await _db.MetricCorrelations.AsNoTracking()
                .Where(c => anomalyIds.Contains(c.SourceAnomalyId))
                .OrderBy(c => c.CorrelatedMetricName)
                .ToListAsync()
                .ConfigureAwait(false);

        var report = new Report
        {
            TenantId = tenantId,
            ReportType = "Incident",
            GeneratedBy = userId.Value,
            Status = "Generating",
            CreatedAt = DateTime.UtcNow,
            FilterStartDate = null,
            FilterEndDate = null,
            SourceIncidentId = incidentId,
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        try
        {
            _ = BuildIncidentReportCsv(incident, correlations);
            report.Status = "Completed";
            report.CompletedAt = DateTime.UtcNow;
            report.BlobUrl = null;
            await _db.SaveChangesAsync().ConfigureAwait(false);

            return ApiResponse<ReportDto>.Ok(await MapReportAsync(report.Id).ConfigureAwait(false));
        }
        catch (Exception)
        {
            report.Status = "Failed";
            report.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync().ConfigureAwait(false);
            return ApiResponse<ReportDto>.Fail("Report generation failed.");
        }
    }

    public async Task<ApiResponse<PagedResult<ReportDto>>> GetReportsAsync(PagedRequest paging)
    {
        paging ??= new PagedRequest();

        var query = _db.Reports.AsNoTracking().Include(r => r.GeneratedByUser).OrderByDescending(r => r.CreatedAt);

        var total = await query.CountAsync().ConfigureAwait(false);
        var rows = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync()
            .ConfigureAwait(false);

        var items = rows.Select(MapReport).ToList();

        return ApiResponse<PagedResult<ReportDto>>.Ok(new PagedResult<ReportDto>
        {
            Items = items,
            TotalCount = total,
            Page = paging.Page,
            PageSize = paging.PageSize,
        });
    }

    public async Task<ApiResponse<byte[]>> DownloadReportAsync(int reportId)
    {
        var tenantId = _tenantContext.TenantId;
        var report = await _db.Reports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reportId && r.TenantId == tenantId)
            .ConfigureAwait(false);

        if (report is null)
        {
            return ApiResponse<byte[]>.Fail("Report not found.");
        }

        if (!string.Equals(report.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<byte[]>.Fail("Report is not available for download.");
        }

        if (string.Equals(report.ReportType, "Dashboard", StringComparison.OrdinalIgnoreCase))
        {
            var rows = await _summaryQuery
                .GetDashboardSummaryAsync(tenantId, report.FilterStartDate, report.FilterEndDate)
                .ConfigureAwait(false);
            return ApiResponse<byte[]>.Ok(BuildDashboardCsv(rows));
        }

        if (string.Equals(report.ReportType, "Incident", StringComparison.OrdinalIgnoreCase)
            && report.SourceIncidentId.HasValue)
        {
            var incident = await _db.Incidents
                .AsNoTracking()
                .Include(i => i.Events)
                .FirstOrDefaultAsync(i => i.Id == report.SourceIncidentId.Value && i.TenantId == tenantId)
                .ConfigureAwait(false);

            if (incident is null)
            {
                return ApiResponse<byte[]>.Fail("Source incident no longer exists.");
            }

            var anomalyIds = await _db.AnomalyScores.AsNoTracking()
                .Where(a => a.IncidentId == incident.Id)
                .Select(a => a.Id)
                .ToListAsync()
                .ConfigureAwait(false);

            var correlations = anomalyIds.Count == 0
                ? new List<MetricCorrelation>()
                : await _db.MetricCorrelations.AsNoTracking()
                    .Where(c => anomalyIds.Contains(c.SourceAnomalyId))
                    .OrderBy(c => c.CorrelatedMetricName)
                    .ToListAsync()
                    .ConfigureAwait(false);

            return ApiResponse<byte[]>.Ok(BuildIncidentReportCsv(incident, correlations));
        }

        return ApiResponse<byte[]>.Fail("Unsupported report type.");
    }

    private async Task<ReportDto> MapReportAsync(int id)
    {
        var r = await _db.Reports.AsNoTracking()
            .Include(x => x.GeneratedByUser)
            .FirstAsync(x => x.Id == id)
            .ConfigureAwait(false);
        return MapReport(r);
    }

    private static ReportDto MapReport(Report r)
    {
        var name = $"{r.GeneratedByUser.FirstName} {r.GeneratedByUser.LastName}".Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = r.GeneratedByUser.Email;
        }

        return new ReportDto
        {
            Id = r.Id,
            ReportType = r.ReportType,
            GeneratedByName = name,
            Status = r.Status,
            DownloadUrl = r.BlobUrl,
            CreatedAt = r.CreatedAt,
            CompletedAt = r.CompletedAt,
        };
    }

    private static byte[] BuildDashboardCsv(IReadOnlyList<MetricSummaryDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', "MetricName", "Category", "LatestValue", "MinValue", "MaxValue", "AvgValue", "DataPointCount", "LatestRecordedAt", "TrendDirection"));
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(
                ',',
                Csv(r.MetricName),
                Csv(r.Category),
                r.LatestValue.ToString(CultureInfo.InvariantCulture),
                r.MinValue.ToString(CultureInfo.InvariantCulture),
                r.MaxValue.ToString(CultureInfo.InvariantCulture),
                r.AvgValue.ToString(CultureInfo.InvariantCulture),
                r.DataPointCount.ToString(CultureInfo.InvariantCulture),
                Csv(r.LatestRecordedAt?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty),
                Csv(r.TrendDirection)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] BuildIncidentReportCsv(Incident incident, IReadOnlyList<MetricCorrelation> correlations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Incident Report");
        sb.AppendLine($"Title,{Csv(incident.Title)}");
        sb.AppendLine($"Severity,{Csv(incident.Severity)}");
        sb.AppendLine($"Status,{Csv(incident.Status)}");
        sb.AppendLine($"StartedAt,{incident.StartedAt:o}");
        sb.AppendLine($"ResolvedAt,{incident.ResolvedAt?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty}");
        sb.AppendLine($"AnomalyCount,{incident.AnomalyCount}");
        sb.AppendLine($"AffectedMetrics,{Csv(incident.AffectedMetrics)}");
        sb.AppendLine();
        sb.AppendLine("Timeline");
        sb.AppendLine("Time,EventType,Description,MetricName,MetricValue");
        foreach (var ev in incident.Events.OrderBy(e => e.CreatedAt))
        {
            sb.AppendLine(string.Join(
                ',',
                ev.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                Csv(ev.EventType),
                Csv(ev.Description),
                Csv(ev.MetricName ?? string.Empty),
                ev.MetricValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
        }

        sb.AppendLine();
        sb.AppendLine("Correlations");
        sb.AppendLine("CorrelatedMetric,Value,ZScore,TimeOffsetSeconds,DetectedAt");
        foreach (var c in correlations)
        {
            sb.AppendLine(string.Join(
                ',',
                Csv(c.CorrelatedMetricName),
                c.CorrelatedMetricValue.ToString(CultureInfo.InvariantCulture),
                c.CorrelatedZScore.ToString(CultureInfo.InvariantCulture),
                c.TimeOffsetSeconds.ToString(CultureInfo.InvariantCulture),
                c.DetectedAt.ToString("o", CultureInfo.InvariantCulture)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Csv(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
        {
            return $"\"{v.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return v;
    }
}
