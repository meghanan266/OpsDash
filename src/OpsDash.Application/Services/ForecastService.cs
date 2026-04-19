using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsDash.Application.Configuration;
using OpsDash.Application.DTOs.Metrics;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Services;

public sealed class ForecastService : IForecastService
{
    private const decimal Z975 = 1.96m;
    private const int MinDataPoints = 5;

    private readonly IAppDbContext _db;
    private readonly ITenantContextService _tenantContext;
    private readonly ForecastSettings _settings;
    private readonly ILogger<ForecastService> _logger;

    public ForecastService(
        IAppDbContext db,
        ITenantContextService tenantContext,
        IOptions<ForecastSettings> options,
        ILogger<ForecastService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<List<ForecastPointDto>> GenerateForecastAsync(string metricName, string? method = null, int? horizon = null)
    {
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return [];
        }

        var tenantId = _tenantContext.TenantId;
        var resolvedMethod = ResolveMethod(method);
        var h = horizon is > 0 and <= 365 ? horizon.Value : Math.Max(1, _settings.ForecastHorizon);
        var take = Math.Max(MinDataPoints, _settings.DefaultDataPoints);

        var rows = await _db.Metrics.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.MetricName == metricName)
            .OrderByDescending(m => m.RecordedAt)
            .Take(take)
            .ToListAsync();

        if (rows.Count < MinDataPoints)
        {
            return [];
        }

        rows.Reverse();
        var values = rows.Select(r => r.MetricValue).ToList();
        var lastRecordedAt = rows[^1].RecordedAt;
        var avgIntervalSeconds = AverageIntervalSeconds(rows);

        return resolvedMethod == "LinearRegression"
            ? GenerateLinearRegression(metricName, values, lastRecordedAt, avgIntervalSeconds, h)
            : GenerateWma(metricName, values, lastRecordedAt, avgIntervalSeconds, h);
    }

    public async Task StoreForecastAsync(int tenantId, string metricName, List<ForecastPointDto> forecasts)
    {
        EnsureTenant(tenantId);

        if (string.IsNullOrWhiteSpace(metricName))
        {
            throw new ArgumentException("Metric name is required.", nameof(metricName));
        }

        var now = DateTime.UtcNow;
        var stale = await _db.MetricForecasts
            .Where(f => f.TenantId == tenantId && f.MetricName == metricName && f.ForecastedFor > now)
            .ToListAsync();

        foreach (var s in stale)
        {
            _db.MetricForecasts.Remove(s);
        }

        foreach (var dto in forecasts)
        {
            _db.MetricForecasts.Add(new MetricForecast
            {
                TenantId = tenantId,
                MetricName = metricName,
                ForecastedValue = Round4(dto.ForecastedValue),
                ForecastMethod = dto.ForecastMethod,
                ForecastedFor = dto.ForecastedFor,
                ConfidenceLower = dto.ConfidenceLower.HasValue ? Round4(dto.ConfidenceLower.Value) : null,
                ConfidenceUpper = dto.ConfidenceUpper.HasValue ? Round4(dto.ConfidenceUpper.Value) : null,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation(
            "Stored {Count} forecast points for tenant {TenantId} metric {MetricName} (removed {Removed} stale future rows).",
            forecasts.Count,
            tenantId,
            metricName,
            stale.Count);
    }

    private List<ForecastPointDto> GenerateWma(
        string metricName,
        List<decimal> values,
        DateTime lastRecordedAt,
        decimal avgIntervalSeconds,
        int horizon)
    {
        var n = values.Count;
        var window = new List<decimal>(values);
        var stdResidual = ResidualStdFromFirstDifferences(values);
        var results = new List<ForecastPointDto>();

        for (var period = 0; period < horizon; period++)
        {
            var forecastValue = WeightedMovingAverage(window);
            var periodsAhead = period + 1;
            var margin = Z975 * stdResidual * SqrtDecimal(periodsAhead);
            var lower = forecastValue - margin;
            var upper = forecastValue + margin;

            results.Add(new ForecastPointDto
            {
                MetricName = metricName,
                ForecastedValue = Round4(forecastValue),
                ForecastMethod = "WMA",
                ForecastedFor = AddAverageInterval(lastRecordedAt, avgIntervalSeconds, periodsAhead),
                ConfidenceLower = Round4(lower),
                ConfidenceUpper = Round4(upper),
            });

            window.RemoveAt(0);
            window.Add(forecastValue);
        }

        return results;
    }

    private List<ForecastPointDto> GenerateLinearRegression(
        string metricName,
        List<decimal> values,
        DateTime lastRecordedAt,
        decimal avgIntervalSeconds,
        int horizon)
    {
        var n = values.Count;
        if (n < MinDataPoints)
        {
            return [];
        }

        decimal sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        for (var i = 0; i < n; i++)
        {
            var x = (decimal)i;
            var y = values[i];
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
        }

        var denom = n * sumX2 - sumX * sumX;
        if (denom == 0)
        {
            return [];
        }

        var m = (n * sumXY - sumX * sumY) / denom;
        var b = (sumY - m * sumX) / n;
        var meanX = sumX / n;

        decimal ssX = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = (decimal)i - meanX;
            ssX += dx * dx;
        }

        decimal sse = 0;
        for (var i = 0; i < n; i++)
        {
            var pred = m * i + b;
            var err = values[i] - pred;
            sse += err * err;
        }

        var degFree = Math.Max(1, n - 2);
        var variance = sse / degFree;
        var stdError = SqrtDecimal(variance);
        if (stdError < 0.0001m)
        {
            stdError = 0.0001m;
        }

        var results = new List<ForecastPointDto>();
        for (var periodIndex = 0; periodIndex < horizon; periodIndex++)
        {
            var forecastX = (decimal)(n + periodIndex);
            var forecastValue = m * forecastX + b;
            var dx = forecastX - meanX;
            var inner = 1m + 1m / n;
            if (ssX > 0)
            {
                inner += (dx * dx) / ssX;
            }

            var margin = Z975 * stdError * SqrtDecimal(inner);
            var lower = forecastValue - margin;
            var upper = forecastValue + margin;

            results.Add(new ForecastPointDto
            {
                MetricName = metricName,
                ForecastedValue = Round4(forecastValue),
                ForecastMethod = "LinearRegression",
                ForecastedFor = AddAverageInterval(lastRecordedAt, avgIntervalSeconds, periodIndex + 1),
                ConfidenceLower = Round4(lower),
                ConfidenceUpper = Round4(upper),
            });
        }

        return results;
    }

    private static decimal WeightedMovingAverage(IReadOnlyList<decimal> window)
    {
        var win = window.Count;
        var denom = win * (win + 1m) / 2m;
        decimal sum = 0;
        for (var i = 0; i < win; i++)
        {
            var w = (i + 1m) / denom;
            sum += window[i] * w;
        }

        return sum;
    }

    private static decimal ResidualStdFromFirstDifferences(IReadOnlyList<decimal> values)
    {
        if (values.Count < 2)
        {
            return 0.0001m;
        }

        var diffs = new List<decimal>(values.Count - 1);
        for (var i = 1; i < values.Count; i++)
        {
            diffs.Add(values[i] - values[i - 1]);
        }

        var s = PopulationStandardDeviation(diffs);
        return s < 0.0001m ? 0.0001m : s;
    }

    private static decimal PopulationStandardDeviation(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var mean = values.Average();
        decimal sumSq = 0;
        foreach (var v in values)
        {
            var d = v - mean;
            sumSq += d * d;
        }

        return SqrtDecimal(sumSq / values.Count);
    }

    private static decimal AverageIntervalSeconds(IReadOnlyList<Metric> chronological)
    {
        if (chronological.Count < 2)
        {
            return 3600m;
        }

        decimal total = 0;
        for (var i = 1; i < chronological.Count; i++)
        {
            total += (decimal)(chronological[i].RecordedAt - chronological[i - 1].RecordedAt).TotalSeconds;
        }

        return total / (chronological.Count - 1);
    }

    private static DateTime AddAverageInterval(DateTime origin, decimal avgIntervalSeconds, int periodsAhead)
    {
        var totalSeconds = avgIntervalSeconds * periodsAhead;
        var ticks = decimal.Round(totalSeconds * (decimal)TimeSpan.TicksPerSecond, 0, MidpointRounding.AwayFromZero);
        return origin.AddTicks((long)ticks);
    }

    private static decimal SqrtDecimal(decimal value)
    {
        if (value <= 0)
        {
            return 0;
        }

        return (decimal)Math.Sqrt((double)value);
    }

    private static decimal Round4(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private string ResolveMethod(string? method)
    {
        var raw = string.IsNullOrWhiteSpace(method) ? _settings.DefaultMethod : method!;
        if (raw.Trim().Equals("LinearRegression", StringComparison.OrdinalIgnoreCase))
        {
            return "LinearRegression";
        }

        return "WMA";
    }

    private void EnsureTenant(int tenantId)
    {
        if (tenantId != _tenantContext.TenantId)
        {
            throw new ArgumentException("The tenant id does not match the current tenant context.", nameof(tenantId));
        }
    }
}
