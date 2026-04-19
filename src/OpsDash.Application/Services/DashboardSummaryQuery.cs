using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpsDash.Application.DTOs.Metrics;
using OpsDash.Application.Interfaces;

namespace OpsDash.Application.Services;

public sealed class DashboardSummaryQuery : IDashboardSummaryQuery
{
    private readonly IAppDbContext _db;

    public DashboardSummaryQuery(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<List<MetricSummaryDto>> GetDashboardSummaryAsync(
        int tenantId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        const string sql = "EXEC [dbo].[sp_GetDashboardSummary] @TenantId, @StartDate, @EndDate";

        return await _db.Database
            .SqlQueryRaw<MetricSummaryDto>(
                sql,
                new SqlParameter("@TenantId", tenantId),
                new SqlParameter("@StartDate", startDate.HasValue ? startDate.Value : DBNull.Value),
                new SqlParameter("@EndDate", endDate.HasValue ? endDate.Value : DBNull.Value))
            .ToListAsync(cancellationToken);
    }
}
