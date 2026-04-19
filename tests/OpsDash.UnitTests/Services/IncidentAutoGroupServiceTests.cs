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

public sealed class IncidentAutoGroupServiceTests
{
    private static IncidentAutoGroupService CreateSut(
        List<AnomalyScore> anomalies,
        List<Incident> incidents,
        List<IncidentEvent> events,
        List<MetricCorrelation> correlations,
        AnomalyDetectionSettings? settings = null,
        int tenantId = 1)
    {
        var anomaliesMock = MockDbSetHelper.CreateMockDbSet(anomalies);
        var incidentsMock = MockDbSetHelper.CreateMockDbSet(incidents);
        var eventsMock = MockDbSetHelper.CreateMockDbSet(events);
        var correlationsMock = MockDbSetHelper.CreateMockDbSet(correlations);

        var db = new Mock<IAppDbContext>();
        db.Setup(x => x.AnomalyScores).Returns(anomaliesMock.Object);
        db.Setup(x => x.Incidents).Returns(incidentsMock.Object);
        db.Setup(x => x.IncidentEvents).Returns(eventsMock.Object);
        db.Setup(x => x.MetricCorrelations).Returns(correlationsMock.Object);

        var nextIncidentId = 200;
        db.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Callback(() =>
            {
                foreach (var inc in incidents.Where(i => i.Id == 0))
                {
                    inc.Id = nextIncidentId++;
                }
            });

        var tenant = new Mock<ITenantContextService>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        var realtime = new Mock<IRealtimeNotificationService>();

        return new IncidentAutoGroupService(
            db.Object,
            tenant.Object,
            Options.Create(settings ?? new AnomalyDetectionSettings { IncidentGroupingWindowMinutes = 30 }),
            realtime.Object,
            NullLogger<IncidentAutoGroupService>.Instance);
    }

    private static AnomalyScore Anomaly(long id, DateTime detectedAt, string metricName = "cpu")
    {
        return new AnomalyScore
        {
            Id = id,
            TenantId = 1,
            MetricId = id,
            MetricName = metricName,
            MetricValue = 42,
            ZScore = 3.1m,
            Severity = "Critical",
            BaselineMean = 10,
            BaselineStdDev = 1,
            DetectedAt = detectedAt,
            IsActive = true,
        };
    }

    [Fact]
    public async Task ProcessAnomaly_NoExistingIncident_CreatesNew()
    {
        var detectedAt = new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);
        var anomalies = new List<AnomalyScore> { Anomaly(1, detectedAt) };
        var incidents = new List<Incident>();
        var events = new List<IncidentEvent>();
        var correlations = new List<MetricCorrelation>();

        var sut = CreateSut(anomalies, incidents, events, correlations);

        var incidentId = await sut.ProcessAnomalyForIncidentAsync(1);

        incidentId.Should().NotBeNull();
        incidents.Should().ContainSingle();
        incidents[0].Id.Should().Be(incidentId);
        anomalies[0].IncidentId.Should().Be(incidentId);
        events.Should().NotBeEmpty();
        events.Should().Contain(e => e.EventType == "AnomalyDetected");
    }

    [Fact]
    public async Task ProcessAnomaly_ExistingOpenIncident_AddsToExisting()
    {
        var detectedAt = new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);
        var existing = new Incident
        {
            Id = 55,
            TenantId = 1,
            Title = "Open",
            Severity = "Warning",
            Status = "Open",
            AnomalyCount = 1,
            AffectedMetrics = "[\"cpu\"]",
            StartedAt = detectedAt.AddMinutes(-10),
        };
        var anomalies = new List<AnomalyScore> { Anomaly(1, detectedAt) };
        var incidents = new List<Incident> { existing };
        var events = new List<IncidentEvent>();
        var correlations = new List<MetricCorrelation>();

        var sut = CreateSut(anomalies, incidents, events, correlations);

        var incidentId = await sut.ProcessAnomalyForIncidentAsync(1);

        incidentId.Should().Be(55);
        existing.AnomalyCount.Should().Be(2);
        anomalies[0].IncidentId.Should().Be(55);
        events.Should().Contain(e => e.IncidentId == 55 && e.EventType == "AnomalyDetected");
    }

    [Fact]
    public async Task ProcessAnomaly_ExistingResolvedIncident_CreatesNew()
    {
        var detectedAt = new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);
        var resolved = new Incident
        {
            Id = 99,
            TenantId = 1,
            Title = "Old",
            Severity = "Critical",
            Status = "Resolved",
            AnomalyCount = 3,
            AffectedMetrics = "[\"cpu\"]",
            StartedAt = detectedAt.AddMinutes(-5),
            ResolvedAt = detectedAt.AddMinutes(-1),
        };
        var anomalies = new List<AnomalyScore> { Anomaly(1, detectedAt) };
        var incidents = new List<Incident> { resolved };
        var events = new List<IncidentEvent>();
        var correlations = new List<MetricCorrelation>();

        var sut = CreateSut(anomalies, incidents, events, correlations);

        var incidentId = await sut.ProcessAnomalyForIncidentAsync(1);

        incidentId.Should().NotBe(99);
        incidents.Should().HaveCount(2);
        anomalies[0].IncidentId.Should().Be(incidentId);
    }

    [Fact]
    public async Task ProcessAnomaly_IncidentOutsideWindow_CreatesNew()
    {
        var detectedAt = new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);
        var tooOld = new Incident
        {
            Id = 77,
            TenantId = 1,
            Title = "Stale",
            Severity = "Warning",
            Status = "Open",
            AnomalyCount = 1,
            AffectedMetrics = "[\"cpu\"]",
            StartedAt = detectedAt.AddMinutes(-45),
        };
        var anomalies = new List<AnomalyScore> { Anomaly(1, detectedAt) };
        var incidents = new List<Incident> { tooOld };
        var events = new List<IncidentEvent>();
        var correlations = new List<MetricCorrelation>();

        var sut = CreateSut(anomalies, incidents, events, correlations);

        var incidentId = await sut.ProcessAnomalyForIncidentAsync(1);

        incidentId.Should().NotBe(77);
        incidents.Should().HaveCount(2);
    }

    [Fact]
    public async Task CheckAutoResolve_AllAnomaliesResolved_ResolvesIncident()
    {
        var t0 = new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc);
        var incident = new Incident
        {
            Id = 10,
            TenantId = 1,
            Title = "Test",
            Severity = "Critical",
            Status = "Open",
            AnomalyCount = 2,
            AffectedMetrics = "[\"cpu\"]",
            StartedAt = t0,
        };
        var anomalies = new List<AnomalyScore>
        {
            new()
            {
                Id = 1,
                TenantId = 1,
                MetricId = 1,
                MetricName = "cpu",
                MetricValue = 1,
                ZScore = 2,
                Severity = "Warning",
                BaselineMean = 0,
                BaselineStdDev = 1,
                DetectedAt = t0,
                IsActive = false,
                IncidentId = 10,
            },
            new()
            {
                Id = 2,
                TenantId = 1,
                MetricId = 2,
                MetricName = "mem",
                MetricValue = 2,
                ZScore = 2,
                Severity = "Warning",
                BaselineMean = 0,
                BaselineStdDev = 1,
                DetectedAt = t0,
                IsActive = false,
                IncidentId = 10,
            },
        };
        var incidents = new List<Incident> { incident };
        var events = new List<IncidentEvent>();
        var correlations = new List<MetricCorrelation>();

        var sut = CreateSut(anomalies, incidents, events, correlations);

        await sut.CheckAndAutoResolveIncidentsAsync();

        incident.Status.Should().Be("Resolved");
        incident.ResolvedAt.Should().NotBeNull();
        events.Should().Contain(e => e.EventType == "Resolved");
    }

    [Fact]
    public async Task CheckAutoResolve_SomeAnomaliesActive_KeepsOpen()
    {
        var t0 = new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc);
        var incident = new Incident
        {
            Id = 10,
            TenantId = 1,
            Title = "Test",
            Severity = "Critical",
            Status = "Open",
            AnomalyCount = 2,
            AffectedMetrics = "[\"cpu\"]",
            StartedAt = t0,
        };
        var anomalies = new List<AnomalyScore>
        {
            new()
            {
                Id = 1,
                TenantId = 1,
                MetricId = 1,
                MetricName = "cpu",
                MetricValue = 1,
                ZScore = 2,
                Severity = "Warning",
                BaselineMean = 0,
                BaselineStdDev = 1,
                DetectedAt = t0,
                IsActive = false,
                IncidentId = 10,
            },
            new()
            {
                Id = 2,
                TenantId = 1,
                MetricId = 2,
                MetricName = "mem",
                MetricValue = 2,
                ZScore = 2,
                Severity = "Warning",
                BaselineMean = 0,
                BaselineStdDev = 1,
                DetectedAt = t0,
                IsActive = true,
                IncidentId = 10,
            },
        };
        var incidents = new List<Incident> { incident };
        var events = new List<IncidentEvent>();
        var correlations = new List<MetricCorrelation>();

        var sut = CreateSut(anomalies, incidents, events, correlations);

        await sut.CheckAndAutoResolveIncidentsAsync();

        incident.Status.Should().Be("Open");
        events.Should().BeEmpty();
    }
}
