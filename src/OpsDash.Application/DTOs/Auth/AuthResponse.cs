namespace OpsDash.Application.DTOs.Auth;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime TokenExpiration { get; set; }

    public int UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public int TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;
}
