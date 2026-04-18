using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class MetricConfiguration : IEntityTypeConfiguration<Metric>
{
    public void Configure(EntityTypeBuilder<Metric> entity)
    {
        entity.ToTable("Metrics");

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.Property(e => e.MetricName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.MetricValue).HasColumnType("decimal(18,4)");
        entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        entity.HasIndex(e => new { e.TenantId, e.MetricName, e.RecordedAt });
        entity.HasIndex(e => new { e.TenantId, e.Category, e.RecordedAt });

        entity.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
