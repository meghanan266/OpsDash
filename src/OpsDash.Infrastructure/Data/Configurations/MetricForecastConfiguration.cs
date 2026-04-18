using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class MetricForecastConfiguration : IEntityTypeConfiguration<MetricForecast>
{
    public void Configure(EntityTypeBuilder<MetricForecast> entity)
    {
        entity.ToTable("MetricForecasts");

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.Property(e => e.MetricName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.ForecastedValue).HasColumnType("decimal(18,4)");
        entity.Property(e => e.ForecastMethod).IsRequired().HasMaxLength(50);
        entity.Property(e => e.ConfidenceLower).HasColumnType("decimal(18,4)");
        entity.Property(e => e.ConfidenceUpper).HasColumnType("decimal(18,4)");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        entity.HasIndex(e => new { e.TenantId, e.MetricName, e.ForecastedFor });

        entity.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
