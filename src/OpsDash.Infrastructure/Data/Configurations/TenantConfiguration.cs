using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDash.Domain.Entities;

namespace OpsDash.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> entity)
    {
        entity.ToTable("Tenants");

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Subdomain).IsRequired().HasMaxLength(100);
        entity.HasIndex(e => e.Subdomain).IsUnique();

        entity.Property(e => e.Plan).IsRequired().HasMaxLength(50).HasDefaultValue("Starter");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        entity.Property(e => e.IsActive).HasDefaultValue(true);
    }
}
