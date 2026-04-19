using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.SeedData;

public static class MetricSeriesGenerator
{
    private static readonly Random Rng = new(42);

    /// <summary>Builds cart abandonment as a noisy inverse of checkout conversion at each timestamp.</summary>
    public static List<Metric> GenerateCartAbandonmentFromCheckout(
        IReadOnlyList<Metric> checkoutMetrics,
        int tenantId,
        string category,
        IReadOnlyList<AnomalyEvent> cartAnomalies,
        MetricSeriesBehavior behavior)
    {
        var list = new List<Metric>(checkoutMetrics.Count);
        foreach (var c in checkoutMetrics)
        {
            var noise = (decimal)(Rng.NextDouble() * 2.0 - 1.0) * 0.02m;
            var v = 0.62m + (0.034m - c.MetricValue) * 8m + noise;
            if (behavior.WeekendRise && IsWeekend(c.RecordedAt))
            {
                v += 0.02m;
            }

            if (TryGetAnomalyEffect(c.RecordedAt, cartAnomalies, isHourly: true, 0.68m, 0.10m, out var delta))
            {
                v += delta;
            }

            v = Math.Clamp(v, 0.45m, 0.92m);
            list.Add(
                new Metric
                {
                    TenantId = tenantId,
                    MetricName = "cart_abandonment",
                    Category = category,
                    MetricValue = decimal.Round(v, 6),
                    RecordedAt = c.RecordedAt,
                    CreatedAt = DateTime.UtcNow,
                });
        }

        return list;
    }

    /// <summary>Generates metric points from 90 days ago through now at the given frequency.</summary>
    public static List<Metric> GenerateMetricSeries(
        string metricName,
        string category,
        int tenantId,
        decimal baseValue,
        decimal variance,
        string frequency,
        IReadOnlyList<AnomalyEvent> anomalies,
        MetricSeriesBehavior behavior)
    {
        var list = new List<Metric>(frequency.Equals("hourly", StringComparison.OrdinalIgnoreCase) ? 2200 : 100);
        var end = DateTime.UtcNow;
        var startDay = end.Date.AddDays(-90);

        if (frequency.Equals("daily", StringComparison.OrdinalIgnoreCase))
        {
            var dayIndex = 0;
            var dayCount = (end.Date - startDay).Days + 1;
            for (var d = startDay; d <= end.Date; d = d.AddDays(1))
            {
                var recordedAt = DateTime.SpecifyKind(d, DateTimeKind.Utc).AddHours(12);
                if (recordedAt > end)
                {
                    break;
                }

                if (behavior.WeekdayOnlyDeployments && IsWeekend(recordedAt))
                {
                    continue;
                }

                var progress = dayCount > 1 ? dayIndex / (decimal)(dayCount - 1) : 0m;
                var value = ComputePointValue(
                    recordedAt,
                    baseValue,
                    variance,
                    progress,
                    anomalies,
                    behavior,
                    isHourly: false);

                list.Add(CreateMetric(tenantId, metricName, category, value, recordedAt));
                dayIndex++;
            }

            return list;
        }

        if (!frequency.Equals("hourly", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Frequency must be hourly or daily.", nameof(frequency));
        }

        var cursor = new DateTime(startDay.Year, startDay.Month, startDay.Day, 0, 0, 0, DateTimeKind.Utc);
        var totalHours = (decimal)(end - cursor).TotalHours;
        var hourIndex = 0;
        while (cursor <= end)
        {
            var dayFraction = totalHours > 0 ? hourIndex / totalHours : 0m;
            if (behavior.WeekdayOnlyDeployments && IsWeekend(cursor))
            {
                list.Add(CreateMetric(tenantId, metricName, category, 0m, cursor));
                cursor = cursor.AddHours(1);
                hourIndex++;
                continue;
            }

            var value = ComputePointValue(
                cursor,
                baseValue,
                variance,
                dayFraction,
                anomalies,
                behavior,
                isHourly: true);

            list.Add(CreateMetric(tenantId, metricName, category, value, cursor));
            cursor = cursor.AddHours(1);
            hourIndex++;
        }

        return list;
    }

    private static Metric CreateMetric(int tenantId, string metricName, string category, decimal value, DateTime recordedAt) =>
        new()
        {
            TenantId = tenantId,
            MetricName = metricName,
            Category = category,
            MetricValue = decimal.Round(value, 6),
            RecordedAt = recordedAt,
            CreatedAt = DateTime.UtcNow,
        };

    private static decimal ComputePointValue(
        DateTime recordedAt,
        decimal baseValue,
        decimal variance,
        decimal progress01,
        IReadOnlyList<AnomalyEvent> anomalies,
        MetricSeriesBehavior behavior,
        bool isHourly)
    {
        var noise = (decimal)(Rng.NextDouble() * 2.0 - 1.0) * variance * baseValue;
        var trend = baseValue * behavior.LinearTrendTotal * progress01;
        var v = baseValue + noise + trend;

        if (behavior.WeekendDip && IsWeekend(recordedAt))
        {
            v *= (decimal)behavior.WeekendDipFactor;
        }

        if (behavior.WeekendRise && IsWeekend(recordedAt))
        {
            v *= 1.08m;
        }

        if (behavior.MondaySpike && recordedAt.DayOfWeek == DayOfWeek.Monday)
        {
            v *= 1.12m;
        }

        if (behavior.WeekendSpikeMetric && IsWeekend(recordedAt))
        {
            v *= 1.06m;
        }

        if (behavior.WeekdayHigher && recordedAt.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday)
        {
            v *= 1.05m;
        }

        if (TryGetAnomalyEffect(recordedAt, anomalies, isHourly, baseValue, variance, out var anomalyDelta))
        {
            v += anomalyDelta;
        }

        if (behavior.MinValue is { } min && v < min)
        {
            v = min;
        }

        if (behavior.MaxValue is { } max && v > max)
        {
            v = max;
        }

        return v;
    }

    private static bool TryGetAnomalyEffect(
        DateTime recordedAt,
        IReadOnlyList<AnomalyEvent> anomalies,
        bool isHourly,
        decimal baseValue,
        decimal variance,
        out decimal delta)
    {
        foreach (var a in anomalies)
        {
            var anomalyDay = DateTime.UtcNow.Date.AddDays(-a.DaysAgo);
            if (isHourly)
            {
                if (recordedAt.Date != anomalyDay || recordedAt.Hour != a.HourUtc)
                {
                    continue;
                }
            }
            else if (recordedAt.Date != anomalyDay)
            {
                continue;
            }

            var direction = a.IsSpike ? 1m : -1m;
            delta = direction * baseValue * variance * (decimal)a.VarianceMultiplier;
            return true;
        }

        delta = 0;
        return false;
    }

    private static bool IsWeekend(DateTime utc) =>
        utc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}

public sealed class MetricSeriesBehavior
{
    public bool WeekendDip { get; init; }

    public double WeekendDipFactor { get; init; } = 0.7;

    public bool WeekendRise { get; init; }

    public bool WeekdayOnlyDeployments { get; init; }

    public bool MondaySpike { get; init; }

    public bool WeekendSpikeMetric { get; init; }

    public bool WeekdayHigher { get; init; }

    /// <summary>Linear drift across the window as a fraction of <see cref="Metric"/> base (e.g. 0.06 = +6%).</summary>
    public decimal LinearTrendTotal { get; init; }

    public decimal? MinValue { get; init; }

    public decimal? MaxValue { get; init; }
}
