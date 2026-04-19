namespace OpsDash.Infrastructure.Data.SeedData;

/// <summary>Planned anomaly injection for <see cref="MetricSeriesGenerator.GenerateMetricSeries"/>.</summary>
public sealed class AnomalyEvent
{
    public int DaysAgo { get; init; }

    /// <summary>For hourly series, UTC hour (0–23). For daily series, only the date part of <see cref="DaysAgo"/> is used.</summary>
    public int HourUtc { get; init; } = 14;

    public bool IsSpike { get; init; } = true;

    /// <summary>How many times normal variance to inject (typically 3–5).</summary>
    public double VarianceMultiplier { get; init; } = 4.0;
}
