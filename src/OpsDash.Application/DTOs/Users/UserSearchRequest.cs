namespace OpsDash.Application.DTOs.Users;

public class UserSearchRequest
{
    public string? SearchTerm { get; set; }

    public int? RoleId { get; set; }

    public bool? IsActive { get; set; }
}
