using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class IncidentEventConfiguration : IEntityTypeConfiguration<IncidentEvent>
{
    public void Configure(EntityTypeBuilder<IncidentEvent> entity)
    {
        entity.ToTable("IncidentEvents");

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.Property(e => e.EventType).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
        entity.Property(e => e.MetricName).HasMaxLength(200);
        entity.Property(e => e.MetricValue).HasColumnType("decimal(18,4)");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        entity.HasOne(e => e.Incident)
            .WithMany(e => e.Events)
            .HasForeignKey(e => e.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
