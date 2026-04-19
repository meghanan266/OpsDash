using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpsDash.Application.DTOs.Metrics;
using OpsDash.Application.DTOs.Notifications;
using OpsDash.Application.Interfaces;
using OpsDash.Application.Services;
using OpsDash.Domain.Entities;
using OpsDash.UnitTests.Helpers;

namespace OpsDash.UnitTests.Services;

/// <summary>
/// Covers current and predictive alert evaluation (implemented by <see cref="PredictiveAlertService"/>).
/// </summary>
public sealed class AlertServiceTests
{
    private static PredictiveAlertService CreateSut(
        List<Metric> metrics,
        List<AlertRule> rules,
        List<Alert> alerts,
        Mock<IForecastService>? forecast = null,
        int tenantId = 1)
    {
        var metricsMock = MockDbSetHelper.CreateMockDbSet(metrics);
        var rulesMock = MockDbSetHelper.CreateMockDbSet(rules);
        var alertsMock = MockDbSetHelper.CreateMockDbSet(alerts);

        var db = new Mock<IAppDbContext>();
        db.Setup(x => x.Metrics).Returns(metricsMock.Object);
        db.Setup(x => x.AlertRules).Returns(rulesMock.Object);
        db.Setup(x => x.Alerts).Returns(alertsMock.Object);

        var nextAlertId = 1L;
        db.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Callback(() =>
            {
                foreach (var a in alerts.Where(a => a.Id == 0))
                {
                    a.Id = nextAlertId++;
                }
            });

        var tenant = new Mock<ITenantContextService>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        var forecastMock = forecast ?? new Mock<IForecastService>();

        var realtime = new Mock<IRealtimeNotificationService>();
        realtime
            .Setup(r => r.NotifyAlertTriggeredAsync(It.IsAny<int>(), It.IsAny<AlertNotification>()))
            .Returns(Task.CompletedTask);

        return new PredictiveAlertService(
            db.Object,
            tenant.Object,
            forecastMock.Object,
            realtime.Object,
            NullLogger<PredictiveAlertService>.Instance);
    }

    private static DateTime Utc(int y, int m, int d) => new(y, m, d, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EvaluateAlerts_CurrentMode_GreaterThan_Triggers()
    {
        var t0 = Utc(2026, 3, 1);
        var metrics = new List<Metric>
        {
            new()
            {
                Id = 9,
                TenantId = 1,
                MetricName = "cpu",
                MetricValue = 100m,
                Category = "sys",
                RecordedAt = t0,
                CreatedAt = t0,
            },
        };
        var rules = new List<AlertRule>
        {
            new()
            {
                Id = 1,
                TenantId = 1,
                MetricName = "cpu",
                Operator = "GreaterThan",
                Threshold = 50m,
                AlertMode = "Current",
                IsActive = true,
                CreatedBy = 1,
                CreatedAt = t0,
            },
        };
        var alerts = new List<Alert>();

        var sut = CreateSut(metrics, rules, alerts);

        await sut.EvaluateAlertsAsync(9);

        alerts.Should().ContainSingle();
        alerts[0].IsPredictive.Should().BeFalse();
        alerts[0].AlertRuleId.Should().Be(1);
    }

    [Fact]
    public async Task EvaluateAlerts_CurrentMode_LessThan_NoTrigger()
    {
        var t0 = Utc(2026, 3, 2);
        var metrics = new List<Metric>
        {
            new()
            {
                Id = 2,
                TenantId = 1,
                MetricName = "cpu",
                MetricValue = 100m,
                Category = "sys",
                RecordedAt = t0,
                CreatedAt = t0,
            },
        };
        var rules = new List<AlertRule>
        {
            new()
            {
                Id = 1,
                TenantId = 1,
                MetricName = "cpu",
                Operator = "LessThan",
                Threshold = 50m,
                AlertMode = "Current",
                IsActive = true,
                CreatedBy = 1,
                CreatedAt = t0,
            },
        };
        var alerts = new List<Alert>();

        var sut = CreateSut(metrics, rules, alerts);

        await sut.EvaluateAlertsAsync(2);

        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAlerts_Deduplication_SkipsRecentAlert()
    {
        var t0 = Utc(2026, 3, 3);
        var metrics = new List<Metric>
        {
            new()
            {
                Id = 3,
                TenantId = 1,
                MetricName = "cpu",
                MetricValue = 100m,
                Category = "sys",
                RecordedAt = t0,
                CreatedAt = t0,
            },
        };
        var rules = new List<AlertRule>
        {
            new()
            {
                Id = 1,
                TenantId = 1,
                MetricName = "cpu",
                Operator = "GreaterThan",
                Threshold = 50m,
                AlertMode = "Current",
                IsActive = true,
                CreatedBy = 1,
                CreatedAt = t0,
            },
        };
        var alerts = new List<Alert>
        {
            new()
            {
                Id = 50,
                TenantId = 1,
                AlertRuleId = 1,
                MetricValue = 100m,
                IsPredictive = false,
                ForecastedValue = null,
                TriggeredAt = DateTime.UtcNow.AddMinutes(-20),
            },
        };

        var sut = CreateSut(metrics, rules, alerts);

        await sut.EvaluateAlertsAsync(3);

        alerts.Should().ContainSingle();
    }

    [Fact]
    public async Task EvaluatePredictiveAlerts_ForecastBreachesThreshold_Triggers()
    {
        var t0 = Utc(2026, 4, 1);
        var metrics = new List<Metric>
        {
            new()
            {
                Id = 1,
                TenantId = 1,
                MetricName = "cpu",
                MetricValue = 10m,
                Category = "sys",
                RecordedAt = t0,
                CreatedAt = t0,
            },
        };
        var rules = new List<AlertRule>
        {
            new()
            {
                Id = 7,
                TenantId = 1,
                MetricName = "cpu",
                Operator = "GreaterThan",
                Threshold = 50m,
                AlertMode = "Predictive",
                ForecastHorizon = 3,
                IsActive = true,
                CreatedBy = 1,
                CreatedAt = t0,
            },
        };
        var alerts = new List<Alert>();

        var forecast = new Mock<IForecastService>();
        forecast
            .Setup(f => f.GenerateForecastAsync("cpu", null, 3))
            .ReturnsAsync(
            [
                new ForecastPointDto
                {
                    MetricName = "cpu",
                    ForecastedValue = 90m,
                    ForecastMethod = "WMA",
                    ForecastedFor = t0.AddHours(1),
                },
            ]);

        var sut = CreateSut(metrics, rules, alerts, forecast);

        await sut.EvaluatePredictiveAlertsAsync("cpu");

        alerts.Should().ContainSingle();
        alerts[0].IsPredictive.Should().BeTrue();
        alerts[0].ForecastedValue.Should().Be(90m);
    }

    [Fact]
    public async Task EvaluatePredictiveAlerts_NoForecastBreach_NoAlert()
    {
        var t0 = Utc(2026, 4, 2);
        var metrics = new List<Metric>
        {
            new()
            {
                Id = 1,
                TenantId = 1,
                MetricName = "cpu",
                MetricValue = 10m,
                Category = "sys",
                RecordedAt = t0,
                CreatedAt = t0,
            },
        };
        var rules = new List<AlertRule>
        {
            new()
            {
                Id = 7,
                TenantId = 1,
                MetricName = "cpu",
                Operator = "GreaterThan",
                Threshold = 50m,
                AlertMode = "Predictive",
                ForecastHorizon = 3,
                IsActive = true,
                CreatedBy = 1,
                CreatedAt = t0,
            },
        };
        var alerts = new List<Alert>();

        var forecast = new Mock<IForecastService>();
        forecast
            .Setup(f => f.GenerateForecastAsync("cpu", null, 3))
            .ReturnsAsync(
            [
                new ForecastPointDto
                {
                    MetricName = "cpu",
                    ForecastedValue = 20m,
                    ForecastMethod = "WMA",
                    ForecastedFor = t0.AddHours(1),
                },
            ]);

        var sut = CreateSut(metrics, rules, alerts, forecast);

        await sut.EvaluatePredictiveAlertsAsync("cpu");

        alerts.Should().BeEmpty();
    }
}
