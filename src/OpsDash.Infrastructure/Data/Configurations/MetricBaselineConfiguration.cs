using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class MetricBaselineConfiguration : IEntityTypeConfiguration<MetricBaseline>
{
    public void Configure(EntityTypeBuilder<MetricBaseline> entity)
    {
        entity.ToTable("MetricBaselines");

        entity.Property(e => e.MetricName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Mean).HasColumnType("decimal(18,4)");
        entity.Property(e => e.StandardDeviation).HasColumnType("decimal(18,4)");
        entity.Property(e => e.TrendDirection).IsRequired().HasMaxLength(20);

        entity.HasIndex(e => new { e.TenantId, e.MetricName }).IsUnique();

        entity.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
