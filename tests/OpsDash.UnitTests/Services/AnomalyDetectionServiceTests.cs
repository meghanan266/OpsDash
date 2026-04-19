using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpsDash.Application.Configuration;
using OpsDash.Application.DTOs.Anomalies;
using OpsDash.Application.Interfaces;
using OpsDash.Application.Services;
using OpsDash.Domain.Entities;
using OpsDash.UnitTests.Helpers;

namespace OpsDash.UnitTests.Services;

public sealed class AnomalyDetectionServiceTests
{
    private static AnomalyDetectionService CreateSut(
        List<Metric> metrics,
        List<MetricBaseline> baselines,
        List<AnomalyScore> anomalies,
        int tenantId = 1,
        AnomalyDetectionSettings? settings = null)
    {
        var metricsMock = MockDbSetHelper.CreateMockDbSet(metrics);
        var baselinesMock = MockDbSetHelper.CreateMockDbSet(baselines);
        var anomaliesMock = MockDbSetHelper.CreateMockDbSet(anomalies);

        var db = new Mock<IAppDbContext>();
        db.Setup(x => x.Metrics).Returns(metricsMock.Object);
        db.Setup(x => x.MetricBaselines).Returns(baselinesMock.Object);
        db.Setup(x => x.AnomalyScores).Returns(anomaliesMock.Object);

        var nextAnomalyId = 100L;
        var nextBaselineId = 100;
        db.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Callback(() =>
            {
                foreach (var a in anomalies.Where(a => a.Id == 0))
                {
                    a.Id = nextAnomalyId++;
                }

                foreach (var b in baselines.Where(b => b.Id == 0))
                {
                    b.Id = nextBaselineId++;
                }
            });

        var tenant = new Mock<ITenantContextService>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        var correlation = new Mock<ICorrelationService>();
        correlation.Setup(c => c.FindCorrelationsAsync(It.IsAny<long>()))
            .ReturnsAsync(new List<CorrelationResult>());

        return new AnomalyDetectionService(
            db.Object,
            tenant.Object,
            correlation.Object,
            NullLogger<AnomalyDetectionService>.Instance,
            Options.Create(settings ?? new AnomalyDetectionSettings()));
    }

    private static Metric M(long id, decimal value, string name = "cpu", DateTime? recordedAt = null)
    {
        var t = recordedAt ?? DateTime.UtcNow;
        return new Metric
        {
            Id = id,
            TenantId = 1,
            MetricName = name,
            MetricValue = value,
            Category = "cat",
            RecordedAt = t,
            CreatedAt = t,
        };
    }

    [Fact]
    public async Task AnalyzeMetric_WithNormalValue_ReturnsNoAnomaly()
    {
        var t0 = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var metrics = new List<Metric>
        {
            M(1, 100, recordedAt: t0.AddMinutes(-4)),
            M(2, 100, recordedAt: t0.AddMinutes(-3)),
            M(3, 100, recordedAt: t0.AddMinutes(-2)),
            M(4, 100, recordedAt: t0.AddMinutes(-1)),
            M(5, 100, recordedAt: t0),
        };
        var baselines = new List<MetricBaseline>
        {
            new MetricBaseline
            {
                Id = 1,
                TenantId = 1,
                MetricName = "cpu",
                Mean = 100,
                StandardDeviation = 10,
                TrendDirection = "Stable",
                DataPointCount = 5,
                LastCalculatedAt = t0,
            },
        };
        var sut = CreateSut(metrics, baselines, new List<AnomalyScore>());

        var result = await sut.AnalyzeMetricAsync(5);

        result.IsAnomaly.Should().BeFalse();
        result.ZScore.Should().BeApproximately(0m, 0.0001m);
        result.AnomalyScoreId.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeMetric_WithWarningZScore_ReturnsWarning()
    {
        var t0 = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var metrics = new List<Metric>
        {
            M(1, 100, recordedAt: t0.AddMinutes(-4)),
            M(2, 100, recordedAt: t0.AddMinutes(-3)),
            M(3, 100, recordedAt: t0.AddMinutes(-2)),
            M(4, 100, recordedAt: t0.AddMinutes(-1)),
            M(5, 124, recordedAt: t0),
        };
        var baselines = new List<MetricBaseline>
        {
            new MetricBaseline
            {
                Id = 1,
                TenantId = 1,
                MetricName = "cpu",
                Mean = 100,
                StandardDeviation = 10,
                TrendDirection = "Stable",
                DataPointCount = 5,
                LastCalculatedAt = t0,
            },
        };
        var anomalies = new List<AnomalyScore>();
        var sut = CreateSut(metrics, baselines, anomalies);

        var result = await sut.AnalyzeMetricAsync(5);

        result.IsAnomaly.Should().BeTrue();
        result.Severity.Should().Be("Warning");
        result.ZScore.Should().BeApproximately(2.4m, 0.0001m);
        result.AnomalyScoreId.Should().NotBeNull();
        anomalies.Should().ContainSingle();
    }

    [Fact]
    public async Task AnalyzeMetric_WithCriticalZScore_ReturnsCritical()
    {
        var t0 = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var metrics = new List<Metric>
        {
            M(1, 100, recordedAt: t0.AddMinutes(-4)),
            M(2, 100, recordedAt: t0.AddMinutes(-3)),
            M(3, 100, recordedAt: t0.AddMinutes(-2)),
            M(4, 100, recordedAt: t0.AddMinutes(-1)),
            M(5, 127, recordedAt: t0),
        };
        var baselines = new List<MetricBaseline>
        {
            new MetricBaseline
            {
                Id = 1,
                TenantId = 1,
                MetricName = "cpu",
                Mean = 100,
                StandardDeviation = 10,
                TrendDirection = "Stable",
                DataPointCount = 5,
                LastCalculatedAt = t0,
            },
        };
        var sut = CreateSut(metrics, baselines, new List<AnomalyScore>());

        var result = await sut.AnalyzeMetricAsync(5);

        result.IsAnomaly.Should().BeTrue();
        result.Severity.Should().Be("Critical");
        result.ZScore.Should().BeApproximately(2.7m, 0.0001m);
    }

    [Fact]
    public async Task AnalyzeMetric_WithSevereZScore_ReturnsSevere()
    {
        var t0 = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var metrics = new List<Metric>
        {
            M(1, 100, recordedAt: t0.AddMinutes(-4)),
            M(2, 100, recordedAt: t0.AddMinutes(-3)),
            M(3, 100, recordedAt: t0.AddMinutes(-2)),
            M(4, 100, recordedAt: t0.AddMinutes(-1)),
            M(5, 135, recordedAt: t0),
        };
        var baselines = new List<MetricBaseline>
        {
            new MetricBaseline
            {
                Id = 1,
                TenantId = 1,
                MetricName = "cpu",
                Mean = 100,
                StandardDeviation = 10,
                TrendDirection = "Stable",
                DataPointCount = 5,
                LastCalculatedAt = t0,
            },
        };
        var sut = CreateSut(metrics, baselines, new List<AnomalyScore>());

        var result = await sut.AnalyzeMetricAsync(5);

        result.IsAnomaly.Should().BeTrue();
        result.Severity.Should().Be("Severe");
        result.ZScore.Should().BeApproximately(3.5m, 0.0001m);
    }

    [Fact]
    public async Task AnalyzeMetric_WithInsufficientData_SkipsAnalysis()
    {
        var t0 = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var metrics = new List<Metric>
        {
            M(1, 10, recordedAt: t0.AddMinutes(-3)),
            M(2, 20, recordedAt: t0.AddMinutes(-2)),
            M(3, 30, recordedAt: t0.AddMinutes(-1)),
            M(4, 40, recordedAt: t0),
        };
        var sut = CreateSut(metrics, new List<MetricBaseline>(), new List<AnomalyScore>());

        var result = await sut.AnalyzeMetricAsync(4);

        result.IsAnomaly.Should().BeFalse();
        result.ZScore.Should().Be(0);
        result.BaselineMean.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeMetric_WithZeroStdDev_SkipsAnalysis()
    {
        var t0 = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var metrics = new List<Metric>
        {
            M(1, 50, recordedAt: t0.AddMinutes(-4)),
            M(2, 50, recordedAt: t0.AddMinutes(-3)),
            M(3, 50, recordedAt: t0.AddMinutes(-2)),
            M(4, 50, recordedAt: t0.AddMinutes(-1)),
            M(5, 50, recordedAt: t0),
        };
        var baselines = new List<MetricBaseline>
        {
            new MetricBaseline
            {
                Id = 1,
                TenantId = 1,
                MetricName = "cpu",
                Mean = 50,
                StandardDeviation = 0,
                TrendDirection = "Stable",
                DataPointCount = 5,
                LastCalculatedAt = t0,
            },
        };
        var sut = CreateSut(metrics, baselines, new List<AnomalyScore>());

        var result = await sut.AnalyzeMetricAsync(5);

        result.IsAnomaly.Should().BeFalse();
        result.ZScore.Should().Be(0);
    }

    [Fact]
    public async Task UpdateBaseline_ComputesCorrectMean()
    {
        var t0 = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var metrics = new List<Metric>
        {
            M(1, 10, recordedAt: t0.AddMinutes(-2)),
            M(2, 20, recordedAt: t0.AddMinutes(-1)),
            M(3, 30, recordedAt: t0),
        };
        var baselines = new List<MetricBaseline>();
        var sut = CreateSut(metrics, baselines, new List<AnomalyScore>());

        await sut.UpdateBaselineAsync(1, "cpu");

        baselines.Should().ContainSingle();
        baselines[0].Mean.Should().Be(20m);
    }

    [Fact]
    public async Task UpdateBaseline_ComputesCorrectStdDev()
    {
        var t0 = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var metrics = new List<Metric>
        {
            M(1, 10, recordedAt: t0.AddMinutes(-2)),
            M(2, 20, recordedAt: t0.AddMinutes(-1)),
            M(3, 30, recordedAt: t0),
        };
        var baselines = new List<MetricBaseline>();
        var sut = CreateSut(metrics, baselines, new List<AnomalyScore>());

        await sut.UpdateBaselineAsync(1, "cpu");

        var expected = (decimal)Math.Sqrt(200.0 / 3.0);
        baselines[0].StandardDeviation.Should().BeApproximately(expected, 0.0001m);
    }

    [Fact]
    public async Task UpdateBaseline_DetectsRisingTrend()
    {
        var t0 = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var metrics = new List<Metric>();
        for (var i = 0; i < 30; i++)
        {
            metrics.Add(new Metric
            {
                Id = i + 1,
                TenantId = 1,
                MetricName = "cpu",
                MetricValue = i + 1,
                Category = "cat",
                RecordedAt = t0.AddMinutes(i),
                CreatedAt = t0,
            });
        }

        var baselines = new List<MetricBaseline>();
        var sut = CreateSut(metrics, baselines, new List<AnomalyScore>());

        await sut.UpdateBaselineAsync(1, "cpu");

        baselines.Should().ContainSingle();
        baselines[0].TrendDirection.Should().Be("Rising");
    }

    [Fact]
    public async Task UpdateBaseline_DetectsStableTrend()
    {
        var t0 = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var metrics = new List<Metric>();
        for (var i = 0; i < 10; i++)
        {
            metrics.Add(new Metric
            {
                Id = i + 1,
                TenantId = 1,
                MetricName = "cpu",
                MetricValue = 100,
                Category = "cat",
                RecordedAt = t0.AddMinutes(i),
                CreatedAt = t0,
            });
        }

        var baselines = new List<MetricBaseline>();
        var sut = CreateSut(metrics, baselines, new List<AnomalyScore>());

        await sut.UpdateBaselineAsync(1, "cpu");

        baselines[0].TrendDirection.Should().Be("Stable");
    }
}
