using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> entity)
    {
        entity.ToTable("Alerts");

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.Property(e => e.MetricValue).HasColumnType("decimal(18,4)");
        entity.Property(e => e.IsPredictive).HasDefaultValue(false);
        entity.Property(e => e.ForecastedValue).HasColumnType("decimal(18,4)");
        entity.Property(e => e.TriggeredAt).HasDefaultValueSql("GETUTCDATE()");

        entity.HasIndex(e => new { e.TenantId, e.TriggeredAt });

        entity.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.AlertRule)
            .WithMany()
            .HasForeignKey(e => e.AlertRuleId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.AcknowledgedByUser)
            .WithMany()
            .HasForeignKey(e => e.AcknowledgedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
