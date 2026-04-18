namespace OpsDash.Application.DTOs.Auth;

public class TokenRevokeRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
