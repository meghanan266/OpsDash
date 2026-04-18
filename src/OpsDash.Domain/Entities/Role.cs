using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class Role : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Permissions { get; set; } = string.Empty;

    public Tenant Tenant { get; set; } = null!;

    public ICollection<User> Users { get; set; } = new List<User>();
}
