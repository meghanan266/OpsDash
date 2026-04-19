using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpsDash.Application.Interfaces;
using OpsDash.Application.Services;
using OpsDash.Domain.Entities;
using OpsDash.UnitTests.Helpers;

namespace OpsDash.UnitTests.Services;

public sealed class HealthScoreComputeServiceTests
{
    private static HealthScoreComputeService CreateSut(
        List<Metric> metrics,
        List<AnomalyScore> anomalies,
        List<Alert> alerts,
        List<HealthScore> healthScores,
        int tenantId = 1)
    {
        var metricsMock = MockDbSetHelper.CreateMockDbSet(metrics);
        var anomaliesMock = MockDbSetHelper.CreateMockDbSet(anomalies);
        var alertsMock = MockDbSetHelper.CreateMockDbSet(alerts);
        var healthMock = MockDbSetHelper.CreateMockDbSet(healthScores);

        var db = new Mock<IAppDbContext>();
        db.Setup(x => x.Metrics).Returns(metricsMock.Object);
        db.Setup(x => x.AnomalyScores).Returns(anomaliesMock.Object);
        db.Setup(x => x.Alerts).Returns(alertsMock.Object);
        db.Setup(x => x.HealthScores).Returns(healthMock.Object);
        db.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var tenant = new Mock<ITenantContextService>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        var realtime = new Mock<IRealtimeNotificationService>();

        return new HealthScoreComputeService(
            db.Object,
            tenant.Object,
            realtime.Object,
            NullLogger<HealthScoreComputeService>.Instance);
    }

    [Fact]
    public async Task ComputeHealthScore_AllMetricsNormal_ReturnsHighScore()
    {
        var t0 = new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);
        var metrics = new List<Metric>
        {
            new() { Id = 1, TenantId = 1, MetricName = "a", MetricValue = 1, Category = "c", RecordedAt = t0, CreatedAt = t0 },
            new() { Id = 2, TenantId = 1, MetricName = "b", MetricValue = 1, Category = "c", RecordedAt = t0, CreatedAt = t0 },
            new() { Id = 3, TenantId = 1, MetricName = "c", MetricValue = 1, Category = "c", RecordedAt = t0, CreatedAt = t0 },
        };

        var sut = CreateSut(metrics, [], [], []);

        var score = await sut.ComputeAndStoreHealthScoreAsync();

        score.Should().BeGreaterThan(80m);
    }

    [Fact]
    public async Task ComputeHealthScore_MultipleActiveAnomalies_ReturnsLowScore()
    {
        var t0 = DateTime.UtcNow;
        var metrics = new List<Metric>();
        for (var i = 0; i < 10; i++)
        {
            metrics.Add(new Metric
            {
                Id = i + 1,
                TenantId = 1,
                MetricName = $"m{i}",
                MetricValue = 1,
                Category = "c",
                RecordedAt = t0,
                CreatedAt = t0,
            });
        }

        var anomalies = new List<AnomalyScore>();
        for (var i = 0; i < 10; i++)
        {
            anomalies.Add(new AnomalyScore
            {
                Id = i + 1,
                TenantId = 1,
                MetricId = i + 1,
                MetricName = $"m{i}",
                MetricValue = 99,
                ZScore = 5,
                Severity = "Critical",
                BaselineMean = 0,
                BaselineStdDev = 1,
                DetectedAt = t0,
                IsActive = true,
            });
        }

        var sut = CreateSut(metrics, anomalies, [], []);

        var score = await sut.ComputeAndStoreHealthScoreAsync();

        score.Should().BeLessThan(60m);
    }

    [Fact]
    public async Task ComputeHealthScore_NoMetrics_ReturnsDefault()
    {
        var sut = CreateSut([], [], [], []);

        var score = await sut.ComputeAndStoreHealthScoreAsync();

        score.Should().BeGreaterThanOrEqualTo(80m);
    }
}
