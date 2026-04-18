using OpsDash.Domain.Interfaces;

namespace OpsDash.Domain.Entities;

public class User : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public int RoleId { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;

    public Role Role { get; set; } = null!;
}
