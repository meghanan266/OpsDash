namespace OpsDash.Application.DTOs.Auth;

public class RegisterRequest
{
    public string TenantName { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;
}
