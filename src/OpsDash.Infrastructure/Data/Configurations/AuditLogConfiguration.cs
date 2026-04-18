using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.ToTable("AuditLogs");

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
        entity.Property(e => e.EntityName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.EntityId).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Timestamp).HasDefaultValueSql("GETUTCDATE()");

        entity.HasIndex(e => new { e.TenantId, e.Timestamp });

        entity.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
