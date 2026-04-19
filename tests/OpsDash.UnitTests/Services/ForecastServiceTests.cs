using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpsDash.Application.Configuration;
using OpsDash.Application.DTOs.Metrics;
using OpsDash.Application.Interfaces;
using OpsDash.Application.Services;
using OpsDash.Domain.Entities;
using OpsDash.UnitTests.Helpers;

namespace OpsDash.UnitTests.Services;

public sealed class ForecastServiceTests
{
    private static ForecastService CreateSut(
        List<Metric> metrics,
        List<MetricForecast> forecasts,
        int tenantId = 1,
        ForecastSettings? settings = null)
    {
        var metricsMock = MockDbSetHelper.CreateMockDbSet(metrics);
        var forecastsMock = MockDbSetHelper.CreateMockDbSet(forecasts);

        var db = new Mock<IAppDbContext>();
        db.Setup(x => x.Metrics).Returns(metricsMock.Object);
        db.Setup(x => x.MetricForecasts).Returns(forecastsMock.Object);

        var nextForecastId = 1000L;
        db.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Callback(() =>
            {
                foreach (var f in forecasts.Where(f => f.Id == 0))
                {
                    f.Id = nextForecastId++;
                }
            });

        var tenant = new Mock<ITenantContextService>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        return new ForecastService(
            db.Object,
            tenant.Object,
            Options.Create(settings ?? new ForecastSettings()),
            NullLogger<ForecastService>.Instance);
    }

    private static List<Metric> BuildSeries(int count, decimal startValue, decimal step, string name = "cpu")
    {
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var list = new List<Metric>();
        for (var i = 0; i < count; i++)
        {
            list.Add(new Metric
            {
                Id = i + 1,
                TenantId = 1,
                MetricName = name,
                MetricValue = startValue + step * i,
                Category = "c",
                RecordedAt = t0.AddHours(i),
                CreatedAt = t0,
            });
        }

        return list;
    }

    [Fact]
    public async Task GenerateForecast_WMA_ReturnsCorrectNumberOfPoints()
    {
        var metrics = BuildSeries(30, 100m, 0m);
        var sut = CreateSut(metrics, new List<MetricForecast>());

        var result = await sut.GenerateForecastAsync("cpu", "WMA", 7);

        result.Should().HaveCount(7);
    }

    [Fact]
    public async Task GenerateForecast_WMA_ValuesAreReasonable()
    {
        var metrics = BuildSeries(30, 100m, 0m);
        var sut = CreateSut(metrics, new List<MetricForecast>());

        var result = await sut.GenerateForecastAsync("cpu", "WMA", 7);

        result.Should().OnlyContain(p => Math.Abs((double)(p.ForecastedValue - 100m)) < 0.5);
    }

    [Fact]
    public async Task GenerateForecast_LinearRegression_WithUpwardTrend()
    {
        var metrics = BuildSeries(30, 10m, 10m);
        var sut = CreateSut(metrics, new List<MetricForecast>());

        var result = await sut.GenerateForecastAsync("cpu", "LinearRegression", 7);

        result.Should().HaveCount(7);
        for (var i = 1; i < result.Count; i++)
        {
            result[i].ForecastedValue.Should().BeGreaterThan(result[i - 1].ForecastedValue);
        }
    }

    [Fact]
    public async Task GenerateForecast_LinearRegression_WithDownwardTrend()
    {
        var metrics = BuildSeries(30, 300m, -10m);
        var sut = CreateSut(metrics, new List<MetricForecast>());

        var result = await sut.GenerateForecastAsync("cpu", "LinearRegression", 7);

        result.Should().HaveCount(7);
        for (var i = 1; i < result.Count; i++)
        {
            result[i].ForecastedValue.Should().BeLessThan(result[i - 1].ForecastedValue);
        }
    }

    [Fact]
    public async Task GenerateForecast_InsufficientData_ReturnsEmpty()
    {
        var metrics = BuildSeries(4, 1m, 1m);
        var sut = CreateSut(metrics, new List<MetricForecast>());

        var result = await sut.GenerateForecastAsync("cpu", "WMA", 7);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateForecast_ConfidenceBoundsWiden()
    {
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var metrics = new List<Metric>();
        for (var i = 0; i < 30; i++)
        {
            metrics.Add(new Metric
            {
                Id = i + 1,
                TenantId = 1,
                MetricName = "cpu",
                MetricValue = i % 2 == 0 ? 100m : 200m,
                Category = "c",
                RecordedAt = t0.AddHours(i),
                CreatedAt = t0,
            });
        }

        var sut = CreateSut(metrics, new List<MetricForecast>());

        var result = await sut.GenerateForecastAsync("cpu", "WMA", 7);

        var widths = result
            .Select(p => (p.ConfidenceUpper!.Value - p.ConfidenceLower!.Value))
            .ToList();
        for (var i = 1; i < widths.Count; i++)
        {
            widths[i].Should().BeGreaterThanOrEqualTo(widths[i - 1]);
        }

        widths[^1].Should().BeGreaterThan(widths[0]);
    }

    [Fact]
    public async Task GenerateForecast_DefaultsToWMA()
    {
        var metrics = BuildSeries(30, 50m, 1m);
        var settings = new ForecastSettings { DefaultMethod = "WMA" };
        var sut = CreateSut(metrics, new List<MetricForecast>(), settings: settings);

        var result = await sut.GenerateForecastAsync("cpu", method: null, horizon: 5);

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => p.ForecastMethod == "WMA");
    }

    [Fact]
    public async Task StoreForecast_ClearsOldForecasts()
    {
        var now = DateTime.UtcNow;
        var metrics = BuildSeries(10, 100m, 0m);
        var forecasts = new List<MetricForecast>
        {
            new MetricForecast
            {
                Id = 1,
                TenantId = 1,
                MetricName = "cpu",
                ForecastedValue = 1,
                ForecastMethod = "WMA",
                ForecastedFor = now.AddDays(-2),
                ConfidenceLower = null,
                ConfidenceUpper = null,
                CreatedAt = now.AddDays(-3),
            },
            new MetricForecast
            {
                Id = 2,
                TenantId = 1,
                MetricName = "cpu",
                ForecastedValue = 2,
                ForecastMethod = "WMA",
                ForecastedFor = now.AddDays(2),
                ConfidenceLower = null,
                ConfidenceUpper = null,
                CreatedAt = now,
            },
            new MetricForecast
            {
                Id = 3,
                TenantId = 1,
                MetricName = "cpu",
                ForecastedValue = 3,
                ForecastMethod = "WMA",
                ForecastedFor = now.AddDays(3),
                ConfidenceLower = null,
                ConfidenceUpper = null,
                CreatedAt = now,
            },
        };

        var sut = CreateSut(metrics, forecasts);
        var newPoints = new List<ForecastPointDto>
        {
            new()
            {
                MetricName = "cpu",
                ForecastedValue = 99.1234m,
                ForecastMethod = "WMA",
                ForecastedFor = now.AddDays(5),
                ConfidenceLower = 98m,
                ConfidenceUpper = 100m,
            },
            new()
            {
                MetricName = "cpu",
                ForecastedValue = 101.5678m,
                ForecastMethod = "WMA",
                ForecastedFor = now.AddDays(6),
                ConfidenceLower = 99m,
                ConfidenceUpper = 102m,
            },
        };

        await sut.StoreForecastAsync(1, "cpu", newPoints);

        forecasts.Should().ContainSingle(f => f.ForecastedFor <= now);
        forecasts.Count(f => f.ForecastedFor > now).Should().Be(2);
        forecasts.Should().Contain(f => f.ForecastedFor == newPoints[0].ForecastedFor && f.ForecastedValue == 99.1234m);
        forecasts.Should().Contain(f => f.ForecastedFor == newPoints[1].ForecastedFor && f.ForecastedValue == 101.5678m);
    }
}
