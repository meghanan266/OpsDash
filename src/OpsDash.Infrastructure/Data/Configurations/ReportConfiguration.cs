using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> entity)
    {
        entity.ToTable("Reports");

        entity.Property(e => e.ReportType).IsRequired().HasMaxLength(50);
        entity.Property(e => e.BlobUrl).HasMaxLength(1000);
        entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        entity.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.GeneratedByUser)
            .WithMany()
            .HasForeignKey(e => e.GeneratedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
