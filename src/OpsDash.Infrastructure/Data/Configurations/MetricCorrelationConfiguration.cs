using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class MetricCorrelationConfiguration : IEntityTypeConfiguration<MetricCorrelation>
{
    public void Configure(EntityTypeBuilder<MetricCorrelation> entity)
    {
        entity.ToTable("MetricCorrelations");

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.Property(e => e.CorrelatedMetricName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.CorrelatedMetricValue).HasColumnType("decimal(18,4)");
        entity.Property(e => e.CorrelatedZScore).HasColumnType("decimal(10,4)");
        entity.Property(e => e.DetectedAt).HasDefaultValueSql("GETUTCDATE()");

        entity.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.SourceAnomaly)
            .WithMany()
            .HasForeignKey(e => e.SourceAnomalyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
