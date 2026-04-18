using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> entity)
    {
        entity.ToTable("Incidents");

        entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
        entity.Property(e => e.Severity).IsRequired().HasMaxLength(20);
        entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Open");
        entity.Property(e => e.AnomalyCount).HasDefaultValue(1);
        entity.Property(e => e.AffectedMetrics).IsRequired();

        entity.HasIndex(e => new { e.TenantId, e.Status, e.StartedAt });

        entity.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.AcknowledgedByUser)
            .WithMany()
            .HasForeignKey(e => e.AcknowledgedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(e => e.ResolvedByUser)
            .WithMany()
            .HasForeignKey(e => e.ResolvedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
