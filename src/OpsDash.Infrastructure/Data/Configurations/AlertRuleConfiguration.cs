using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> entity)
    {
        entity.ToTable("AlertRules");

        entity.Property(e => e.MetricName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Threshold).HasColumnType("decimal(18,4)");
        entity.Property(e => e.Operator).IsRequired().HasMaxLength(20);
        entity.Property(e => e.AlertMode).IsRequired().HasMaxLength(20).HasDefaultValue("Current");
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        entity.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
