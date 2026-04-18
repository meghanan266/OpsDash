using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Interfaces;

public interface IAppDbContext
{
    DatabaseFacade Database { get; }

    DbSet<Tenant> Tenants { get; }

    DbSet<Role> Roles { get; }

    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Metric> Metrics { get; }

    DbSet<MetricBaseline> MetricBaselines { get; }

    DbSet<MetricForecast> MetricForecasts { get; }

    DbSet<AnomalyScore> AnomalyScores { get; }

    DbSet<MetricCorrelation> MetricCorrelations { get; }

    DbSet<Incident> Incidents { get; }

    DbSet<IncidentEvent> IncidentEvents { get; }

    DbSet<AlertRule> AlertRules { get; }

    DbSet<Alert> Alerts { get; }

    DbSet<HealthScore> HealthScores { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<Report> Reports { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
