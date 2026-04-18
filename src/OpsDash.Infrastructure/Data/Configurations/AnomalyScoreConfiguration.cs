using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class AnomalyScoreConfiguration : IEntityTypeConfiguration<AnomalyScore>
{
    public void Configure(EntityTypeBuilder<AnomalyScore> entity)
    {
        entity.ToTable("AnomalyScores");

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.Property(e => e.MetricName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.MetricValue).HasColumnType("decimal(18,4)");
        entity.Property(e => e.ZScore).HasColumnType("decimal(10,4)");
        entity.Property(e => e.Severity).IsRequired().HasMaxLength(20);
        entity.Property(e => e.BaselineMean).HasColumnType("decimal(18,4)");
        entity.Property(e => e.BaselineStdDev).HasColumnType("decimal(18,4)");
        entity.Property(e => e.DetectedAt).HasDefaultValueSql("GETUTCDATE()");
        entity.Property(e => e.IsActive).HasDefaultValue(true);

        entity.HasIndex(e => new { e.TenantId, e.IsActive, e.DetectedAt });

        entity.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Metric)
            .WithMany()
            .HasForeignKey(e => e.MetricId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Incident)
            .WithMany(e => e.Anomalies)
            .HasForeignKey(e => e.IncidentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
