using Microsoft.EntityFrameworkCore;
using OpsDash.Application.Interfaces;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantContextService _tenantContextService;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContextService tenantContextService)
        : base(options)
    {
        _tenantContextService = tenantContextService;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Metric> Metrics => Set<Metric>();

    public DbSet<MetricBaseline> MetricBaselines => Set<MetricBaseline>();

    public DbSet<MetricForecast> MetricForecasts => Set<MetricForecast>();

    public DbSet<AnomalyScore> AnomalyScores => Set<AnomalyScore>();

    public DbSet<MetricCorrelation> MetricCorrelations => Set<MetricCorrelation>();

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<IncidentEvent> IncidentEvents => Set<IncidentEvent>();

    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<HealthScore> HealthScores => Set<HealthScore>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<Role>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<User>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<Metric>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<MetricBaseline>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<MetricForecast>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<AnomalyScore>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<MetricCorrelation>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<Incident>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<IncidentEvent>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<AlertRule>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<Alert>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<HealthScore>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
        modelBuilder.Entity<Report>().HasQueryFilter(e => e.TenantId == _tenantContextService.TenantId);
    }
}
