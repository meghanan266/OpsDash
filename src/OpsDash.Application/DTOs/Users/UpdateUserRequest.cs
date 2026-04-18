namespace OpsDash.Application.DTOs.Users;

public class UpdateUserRequest
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public int? RoleId { get; set; }

    public bool? IsActive { get; set; }
}
