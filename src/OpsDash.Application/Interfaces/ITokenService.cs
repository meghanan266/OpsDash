using System.Security.Claims;
using OpsDash.Domain.Entities;

namespace OpsDash.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user, string roleName, int tenantId);

    string GenerateRefreshToken();

    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
