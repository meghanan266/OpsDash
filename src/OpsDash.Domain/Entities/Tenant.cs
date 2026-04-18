namespace OpsDash.Domain.Entities;

public class Tenant
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;

    public string Plan { get; set; } = "Starter";

    public DateTime CreatedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<User> Users { get; set; } = new List<User>();

    public ICollection<Role> Roles { get; set; } = new List<Role>();
}
