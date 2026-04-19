using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OpsDash.Application.Interfaces;

namespace OpsDash.Infrastructure.Services;

public sealed class HttpContextCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(id, out var parsed) ? parsed : null;
        }
    }
}
