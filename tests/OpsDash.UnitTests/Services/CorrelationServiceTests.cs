using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpsDash.Application.Configuration;
using OpsDash.Application.Interfaces;
using OpsDash.Application.Services;
using OpsDash.Domain.Entities;
using OpsDash.UnitTests.Helpers;

namespace OpsDash.UnitTests.Services;

public sealed class CorrelationServiceTests
{
    private static CorrelationService CreateSut(
        List<Metric> metrics,
        List<MetricBaseline> baselines,
        List<AnomalyScore> anomalies,
        List<MetricCorrelation> correlations,
        int tenantId = 1,
        AnomalyDetectionSettings? settings = null)
    {
        var metricsMock = MockDbSetHelper.CreateMockDbSet(metrics);
        var baselinesMock = MockDbSetHelper.CreateMockDbSet(baselines);
        var anomaliesMock = MockDbSetHelper.CreateMockDbSet(anomalies);
        var correlationsMock = MockDbSetHelper.CreateMockDbSet(correlations);

        var db = new Mock<IAppDbContext>();
        db.Setup(x => x.Metrics).Returns(metricsMock.Object);
        db.Setup(x => x.MetricBaselines).Returns(baselinesMock.Object);
        db.Setup(x => x.AnomalyScores).Returns(anomaliesMock.Object);
        db.Setup(x => x.MetricCorrelations).Returns(correlationsMock.Object);

        var nextCorrelationId = 1L;
        db.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Callback(() =>
            {
                foreach (var c in correlations.Where(c => c.Id == 0))
                {
                    c.Id = nextCorrelationId++;
                }
            });

        var tenant = new Mock<ITenantContextService>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        return new CorrelationService(
            db.Object,
            tenant.Object,
            Options.Create(settings ?? new AnomalyDetectionSettings()),
            NullLogger<CorrelationService>.Instance);
    }

    private static AnomalyScore Anomaly(long id, long metricId, string metricName, DateTime detectedAt)
    {
        return new AnomalyScore
        {
            Id = id,
            TenantId = 1,
            MetricId = metricId,
            MetricName = metricName,
            MetricValue = 0,
            ZScore = 0,
            Severity = "Warning",
            BaselineMean = 0,
            BaselineStdDev = 1,
            DetectedAt = detectedAt,
            IsActive = true,
        };
    }

    [Fact]
    public async Task FindCorrelations_WithCorrelatedMetrics_ReturnsCorrelations()
    {
        var t0 = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var anomalies = new List<AnomalyScore> { Anomaly(1, 10, "metric_a", t0) };
        var metrics = new List<Metric>
        {
            new Metric
            {
                Id = 10,
                TenantId = 1,
                MetricName = "metric_a",
                MetricValue = 200,
                Category = "c",
                RecordedAt = t0,
                CreatedAt = t0,
            },
            new Metric
            {
                Id = 11,
                TenantId = 1,
                MetricName = "metric_b",
                MetricValue = 130,
                Category = "c",
                RecordedAt = t0.AddMinutes(1),
                CreatedAt = t0,
            },
        };
        var baselines = new List<MetricBaseline>
        {
            new MetricBaseline
            {
                Id = 1,
                TenantId = 1,
                MetricName = "metric_b",
                Mean = 100,
                StandardDeviation = 10,
                TrendDirection = "Stable",
                DataPointCount = 10,
                LastCalculatedAt = t0,
            },
        };
        var correlations = new List<MetricCorrelation>();
        var sut = CreateSut(metrics, baselines, anomalies, correlations);

        var results = await sut.FindCorrelationsAsync(1);

        results.Should().ContainSingle();
        results[0].CorrelatedMetricName.Should().Be("metric_b");
        results[0].CorrelatedMetricValue.Should().Be(130);
        results[0].CorrelatedZScore.Should().BeApproximately(3m, 0.0001m);
        results[0].TimeOffsetSeconds.Should().Be(60);
        correlations.Should().ContainSingle();
    }

    [Fact]
    public async Task FindCorrelations_WithNoCorrelatedMetrics_ReturnsEmpty()
    {
        var t0 = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var anomalies = new List<AnomalyScore> { Anomaly(1, 10, "metric_a", t0) };
        var metrics = new List<Metric>
        {
            new Metric
            {
                Id = 10,
                TenantId = 1,
                MetricName = "metric_a",
                MetricValue = 100,
                Category = "c",
                RecordedAt = t0,
                CreatedAt = t0,
            },
            new Metric
            {
                Id = 11,
                TenantId = 1,
                MetricName = "metric_b",
                MetricValue = 101,
                Category = "c",
                RecordedAt = t0.AddMinutes(1),
                CreatedAt = t0,
            },
        };
        var baselines = new List<MetricBaseline>
        {
            new MetricBaseline
            {
                Id = 1,
                TenantId = 1,
                MetricName = "metric_b",
                Mean = 100,
                StandardDeviation = 10,
                TrendDirection = "Stable",
                DataPointCount = 10,
                LastCalculatedAt = t0,
            },
        };
        var sut = CreateSut(metrics, baselines, anomalies, new List<MetricCorrelation>());

        var results = await sut.FindCorrelationsAsync(1);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FindCorrelations_IgnoresSameMetric()
    {
        var t0 = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var anomalies = new List<AnomalyScore> { Anomaly(1, 10, "metric_a", t0) };
        var metrics = new List<Metric>
        {
            new Metric
            {
                Id = 10,
                TenantId = 1,
                MetricName = "metric_a",
                MetricValue = 100,
                Category = "c",
                RecordedAt = t0,
                CreatedAt = t0,
            },
            new Metric
            {
                Id = 11,
                TenantId = 1,
                MetricName = "metric_a",
                MetricValue = 500,
                Category = "c",
                RecordedAt = t0.AddMinutes(1),
                CreatedAt = t0,
            },
        };
        var baselines = new List<MetricBaseline>
        {
            new MetricBaseline
            {
                Id = 1,
                TenantId = 1,
                MetricName = "metric_a",
                Mean = 100,
                StandardDeviation = 10,
                TrendDirection = "Stable",
                DataPointCount = 10,
                LastCalculatedAt = t0,
            },
        };
        var sut = CreateSut(metrics, baselines, anomalies, new List<MetricCorrelation>());

        var results = await sut.FindCorrelationsAsync(1);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FindCorrelations_RespectsTimeWindow()
    {
        var t0 = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var anomalies = new List<AnomalyScore> { Anomaly(1, 10, "metric_a", t0) };
        var metrics = new List<Metric>
        {
            new Metric
            {
                Id = 10,
                TenantId = 1,
                MetricName = "metric_a",
                MetricValue = 100,
                Category = "c",
                RecordedAt = t0,
                CreatedAt = t0,
            },
            new Metric
            {
                Id = 11,
                TenantId = 1,
                MetricName = "metric_b",
                MetricValue = 200,
                Category = "c",
                RecordedAt = t0.AddMinutes(20),
                CreatedAt = t0,
            },
        };
        var baselines = new List<MetricBaseline>
        {
            new MetricBaseline
            {
                Id = 1,
                TenantId = 1,
                MetricName = "metric_b",
                Mean = 100,
                StandardDeviation = 10,
                TrendDirection = "Stable",
                DataPointCount = 10,
                LastCalculatedAt = t0,
            },
        };
        var settings = new AnomalyDetectionSettings { CorrelationWindowMinutes = 15 };
        var sut = CreateSut(metrics, baselines, anomalies, new List<MetricCorrelation>(), settings: settings);

        var results = await sut.FindCorrelationsAsync(1);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FindCorrelations_SelectsHighestZScore()
    {
        var t0 = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var anomalies = new List<AnomalyScore> { Anomaly(1, 10, "metric_a", t0) };
        var metrics = new List<Metric>
        {
            new Metric
            {
                Id = 10,
                TenantId = 1,
                MetricName = "metric_a",
                MetricValue = 100,
                Category = "c",
                RecordedAt = t0,
                CreatedAt = t0,
            },
            new Metric
            {
                Id = 11,
                TenantId = 1,
                MetricName = "metric_b",
                MetricValue = 117,
                Category = "c",
                RecordedAt = t0.AddMinutes(1),
                CreatedAt = t0,
            },
            new Metric
            {
                Id = 12,
                TenantId = 1,
                MetricName = "metric_b",
                MetricValue = 126,
                Category = "c",
                RecordedAt = t0.AddMinutes(2),
                CreatedAt = t0,
            },
        };
        var baselines = new List<MetricBaseline>
        {
            new MetricBaseline
            {
                Id = 1,
                TenantId = 1,
                MetricName = "metric_b",
                Mean = 100,
                StandardDeviation = 10,
                TrendDirection = "Stable",
                DataPointCount = 10,
                LastCalculatedAt = t0,
            },
        };
        var sut = CreateSut(metrics, baselines, anomalies, new List<MetricCorrelation>());

        var results = await sut.FindCorrelationsAsync(1);

        results.Should().ContainSingle();
        results[0].CorrelatedMetricValue.Should().Be(126);
        results[0].CorrelatedZScore.Should().BeApproximately(2.6m, 0.0001m);
    }

    [Fact]
    public async Task FindCorrelations_SkipsMetricsWithNoBaseline()
    {
        var t0 = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var anomalies = new List<AnomalyScore> { Anomaly(1, 10, "metric_a", t0) };
        var metrics = new List<Metric>
        {
            new Metric
            {
                Id = 10,
                TenantId = 1,
                MetricName = "metric_a",
                MetricValue = 100,
                Category = "c",
                RecordedAt = t0,
                CreatedAt = t0,
            },
            new Metric
            {
                Id = 11,
                TenantId = 1,
                MetricName = "metric_b",
                MetricValue = 500,
                Category = "c",
                RecordedAt = t0.AddMinutes(1),
                CreatedAt = t0,
            },
        };
        var sut = CreateSut(metrics, new List<MetricBaseline>(), anomalies, new List<MetricCorrelation>());

        var results = await sut.FindCorrelationsAsync(1);

        results.Should().BeEmpty();
    }
}
