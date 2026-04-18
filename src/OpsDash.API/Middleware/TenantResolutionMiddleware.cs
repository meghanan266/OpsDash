using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpsDash.Application.Interfaces;

namespace OpsDash.API.Middleware;

public class TenantResolutionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var tenantIdValue = context.User.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantIdValue) || !int.TryParse(tenantIdValue, out var tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            var payload = new { statusCode = 401, message = "Tenant context could not be resolved from token" };
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
            return;
        }

        var tenantSetter = context.RequestServices.GetRequiredService<ICurrentTenantSetter>();
        tenantSetter.SetTenantId(tenantId);
        await _next(context);
    }
}
