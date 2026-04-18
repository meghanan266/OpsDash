using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class HealthScoreConfiguration : IEntityTypeConfiguration<HealthScore>
{
    public void Configure(EntityTypeBuilder<HealthScore> entity)
    {
        entity.ToTable("HealthScores");

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.Property(e => e.OverallScore).HasColumnType("decimal(5,2)");
        entity.Property(e => e.NormalMetricPct).HasColumnType("decimal(5,2)");
        entity.Property(e => e.TrendScore).HasColumnType("decimal(5,2)");
        entity.Property(e => e.ResponseScore).HasColumnType("decimal(5,2)");
        entity.Property(e => e.CalculatedAt).HasDefaultValueSql("GETUTCDATE()");

        entity.HasIndex(e => new { e.TenantId, e.CalculatedAt });

        entity.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
