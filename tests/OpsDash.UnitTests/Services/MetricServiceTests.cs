using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpsDash.Application.DTOs.Anomalies;
using OpsDash.Application.DTOs.Common;
using OpsDash.Application.DTOs.Metrics;
using OpsDash.Application.Interfaces;
using OpsDash.Application.Mappings;
using OpsDash.Application.Services;
using OpsDash.Domain.Entities;
using OpsDash.UnitTests.Helpers;

namespace OpsDash.UnitTests.Services;

public sealed class MetricServiceTests
{
    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(c => c.AddProfile<UserMappingProfile>());
        cfg.AssertConfigurationIsValid();
        return cfg.CreateMapper();
    }

    private static MetricService CreateSut(
        List<Metric> metrics,
        List<HealthScore> healthScores,
        Mock<ICacheService>? cache = null,
        Mock<IDashboardSummaryQuery>? dashboard = null)
    {
        var metricsMock = MockDbSetHelper.CreateMockDbSet(metrics);
        var healthMock = MockDbSetHelper.CreateMockDbSet(healthScores);

        var db = new Mock<IAppDbContext>();
        db.Setup(x => x.Metrics).Returns(metricsMock.Object);
        db.Setup(x => x.HealthScores).Returns(healthMock.Object);

        long nextMetricId = 1;
        db.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Callback(() =>
            {
                foreach (var m in metrics.Where(x => x.Id == 0))
                {
                    m.Id = nextMetricId++;
                }
            });

        var tenant = new Mock<ITenantContextService>();
        tenant.Setup(t => t.TenantId).Returns(1);

        var anomaly = new Mock<IAnomalyDetectionService>();
        anomaly
            .Setup(a => a.AnalyzeMetricAsync(It.IsAny<long>()))
            .ReturnsAsync(
                new AnomalyDetectionResult
                {
                    IsAnomaly = false,
                    MetricId = 0,
                    MetricName = string.Empty,
                    MetricValue = 0,
                });

        var predictive = new Mock<IPredictiveAlertService>();
        predictive.Setup(p => p.EvaluateAlertsAsync(It.IsAny<long>())).Returns(Task.CompletedTask);
        predictive.Setup(p => p.EvaluatePredictiveAlertsAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var healthCompute = new Mock<IHealthScoreComputeService>();
        healthCompute.Setup(h => h.ComputeAndStoreHealthScoreAsync()).ReturnsAsync(85m);

        var cacheMock = cache ?? new Mock<ICacheService>();
        cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cacheMock.Setup(c => c.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cacheMock
            .Setup(c => c.GetAsync<List<MetricSummaryDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheResult<List<MetricSummaryDto>> { IsHit = false, Value = null });
        cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<List<MetricSummaryDto>>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dashMock = dashboard ?? new Mock<IDashboardSummaryQuery>();

        return new MetricService(
            db.Object,
            CreateMapper(),
            tenant.Object,
            anomaly.Object,
            predictive.Object,
            healthCompute.Object,
            cacheMock.Object,
            dashMock.Object,
            NullLogger<MetricService>.Instance);
    }

    private static DateTime Utc(int y, int m, int d) => new(y, m, d, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task IngestMetric_SavesAndReturnsDto()
    {
        var metrics = new List<Metric>();
        var health = new List<HealthScore>();
        var sut = CreateSut(metrics, health);

        var res = await sut.IngestMetricAsync(
            new IngestMetricRequest
            {
                MetricName = "cpu",
                MetricValue = 42m,
                Category = "sys",
            });

        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal("cpu", res.Data.MetricName);
        Assert.Equal(42m, res.Data.MetricValue);
        metrics.Should().ContainSingle();
        metrics[0].TenantId.Should().Be(1);
    }

    [Fact]
    public async Task IngestBatch_SavesAllMetrics()
    {
        var metrics = new List<Metric>();
        var health = new List<HealthScore>();
        var sut = CreateSut(metrics, health);

        var res = await sut.IngestBatchAsync(
            new BatchIngestMetricRequest
            {
                Metrics =
                [
                    new IngestMetricRequest { MetricName = "a", MetricValue = 1m, Category = "c" },
                    new IngestMetricRequest { MetricName = "b", MetricValue = 2m, Category = "c" },
                ],
            });

        res.Success.Should().BeTrue();
        res.Data.Should().HaveCount(2);
        metrics.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMetrics_AppliesPagination()
    {
        var t0 = Utc(2026, 5, 1);
        var metrics = Enumerable
            .Range(0, 25)
            .Select(i => new Metric
            {
                Id = i + 1,
                TenantId = 1,
                MetricName = $"m{i}",
                MetricValue = i,
                Category = "c",
                RecordedAt = t0.AddMinutes(i),
                CreatedAt = t0,
            })
            .ToList();

        var sut = CreateSut(metrics, []);

        var res = await sut.GetMetricsAsync(
            null,
            new PagedRequest
            {
                Page = 2,
                PageSize = 10,
                SortBy = "recordedat",
                SortDirection = "desc",
            });

        res.Success.Should().BeTrue();
        res.Data!.Items.Should().HaveCount(10);
        res.Data.Items[0].MetricName.Should().Be("m14");
        res.Data.TotalCount.Should().Be(25);
    }

    [Fact]
    public async Task GetMetricsSummary_ReturnsSummaryData()
    {
        var metrics = new List<Metric>();
        var health = new List<HealthScore>();
        var summaries = new List<MetricSummaryDto>
        {
            new()
            {
                MetricName = "cpu",
                Category = "sys",
                LatestValue = 1m,
                MinValue = 1m,
                MaxValue = 2m,
                AvgValue = 1.5m,
                DataPointCount = 2,
                TrendDirection = "Stable",
            },
        };

        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cache.Setup(c => c.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cache
            .Setup(c => c.GetAsync<List<MetricSummaryDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheResult<List<MetricSummaryDto>> { IsHit = false, Value = null });
        cache
            .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<List<MetricSummaryDto>>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dashboard = new Mock<IDashboardSummaryQuery>();
        dashboard.Setup(d => d.GetDashboardSummaryAsync(1, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(summaries);

        var sut = CreateSut(metrics, health, cache, dashboard);

        var cached = await sut.GetMetricsSummaryAsync(null, null);

        cached.Response.Success.Should().BeTrue();
        cached.Response.Data.Should().HaveCount(1);
        cached.Response.Data![0].MetricName.Should().Be("cpu");
        cached.FromCache.Should().BeFalse();

        cache.Verify(
            c => c.SetAsync(
                It.Is<string>(k => k.StartsWith("dashboard:summary:1:", StringComparison.Ordinal)),
                It.Is<List<MetricSummaryDto>>(l => l.Count == 1),
                It.Is<TimeSpan?>(t => t == TimeSpan.FromMinutes(2)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
