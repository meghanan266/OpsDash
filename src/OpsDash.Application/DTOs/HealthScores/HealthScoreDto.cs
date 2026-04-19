namespace OpsDash.Application.DTOs.HealthScores;

/// <summary>
/// Tenant health snapshot for dashboard display.
/// </summary>
public class HealthScoreDto
{
    public long Id { get; set; }

    public decimal OverallScore { get; set; }

    public decimal NormalMetricPct { get; set; }

    public int ActiveAnomalies { get; set; }

    public decimal TrendScore { get; set; }

    public decimal ResponseScore { get; set; }

    public DateTime CalculatedAt { get; set; }
}
