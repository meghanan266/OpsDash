using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OpsDash.API.Hubs;

[Authorize]
public sealed class OpsDashHub : Hub
{
    private readonly ILogger<OpsDashHub> _logger;

    public OpsDashHub(ILogger<OpsDashHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroupName(tenantId.Value));
        _logger.LogInformation(
            "Client {ConnectionId} joined tenant group {TenantId}",
            Context.ConnectionId,
            tenantId.Value);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, TenantGroupName(tenantId.Value));
            _logger.LogInformation(
                "Client {ConnectionId} left tenant group {TenantId}",
                Context.ConnectionId,
                tenantId.Value);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private int? GetTenantIdFromClaims()
    {
        var value = Context.User?.FindFirst("tenantId")?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }

    internal static string TenantGroupName(int tenantId) => $"tenant_{tenantId}";
}
