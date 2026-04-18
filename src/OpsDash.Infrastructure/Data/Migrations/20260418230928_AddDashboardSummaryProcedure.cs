using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsDash.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddDashboardSummaryProcedure : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER PROCEDURE [dbo].[sp_GetDashboardSummary]
                @TenantId INT,
                @StartDate DATETIME2 = NULL,
                @EndDate DATETIME2 = NULL
            AS
            BEGIN
                SET NOCOUNT ON;

                IF @StartDate IS NULL SET @StartDate = DATEADD(DAY, -30, GETUTCDATE());
                IF @EndDate IS NULL SET @EndDate = GETUTCDATE();

                SELECT
                    m.MetricName,
                    m.Category,
                    latest.MetricValue AS LatestValue,
                    stats.MinValue,
                    stats.MaxValue,
                    stats.AvgValue,
                    stats.DataPointCount,
                    latest.RecordedAt AS LatestRecordedAt,
                    CASE
                        WHEN last5.AvgValue > prev5.AvgValue * 1.02 THEN 'Rising'
                        WHEN last5.AvgValue < prev5.AvgValue * 0.98 THEN 'Falling'
                        ELSE 'Stable'
                    END AS TrendDirection
                FROM (
                    SELECT DISTINCT MetricName, Category
                    FROM Metrics
                    WHERE TenantId = @TenantId AND RecordedAt BETWEEN @StartDate AND @EndDate
                ) m
                CROSS APPLY (
                    SELECT TOP 1 MetricValue, RecordedAt
                    FROM Metrics
                    WHERE TenantId = @TenantId AND MetricName = m.MetricName AND RecordedAt BETWEEN @StartDate AND @EndDate
                    ORDER BY RecordedAt DESC
                ) latest
                CROSS APPLY (
                    SELECT
                        MIN(MetricValue) AS MinValue,
                        MAX(MetricValue) AS MaxValue,
                        AVG(MetricValue) AS AvgValue,
                        COUNT(*) AS DataPointCount
                    FROM Metrics
                    WHERE TenantId = @TenantId AND MetricName = m.MetricName AND RecordedAt BETWEEN @StartDate AND @EndDate
                ) stats
                OUTER APPLY (
                    SELECT AVG(MetricValue) AS AvgValue
                    FROM (
                        SELECT TOP 5 MetricValue
                        FROM Metrics
                        WHERE TenantId = @TenantId AND MetricName = m.MetricName AND RecordedAt BETWEEN @StartDate AND @EndDate
                        ORDER BY RecordedAt DESC
                    ) t
                ) last5
                OUTER APPLY (
                    SELECT AVG(MetricValue) AS AvgValue
                    FROM (
                        SELECT MetricValue
                        FROM Metrics
                        WHERE TenantId = @TenantId AND MetricName = m.MetricName AND RecordedAt BETWEEN @StartDate AND @EndDate
                        ORDER BY RecordedAt DESC
                        OFFSET 5 ROWS FETCH NEXT 5 ROWS ONLY
                    ) t
                ) prev5
                ORDER BY m.Category, m.MetricName;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [dbo].[sp_GetDashboardSummary]");
    }
}
