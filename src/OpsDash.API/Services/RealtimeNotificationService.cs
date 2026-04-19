using Microsoft.AspNetCore.SignalR;
using OpsDash.API.Hubs;
using OpsDash.Application.DTOs.Notifications;
using OpsDash.Application.Interfaces;

namespace OpsDash.API.Services;

public sealed class RealtimeNotificationService : IRealtimeNotificationService
{
    private readonly IHubContext<OpsDashHub> _hubContext;

    public RealtimeNotificationService(IHubContext<OpsDashHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyAnomalyDetectedAsync(int tenantId, AnomalyNotification notification) =>
        _hubContext.Clients.Group(OpsDashHub.TenantGroupName(tenantId)).SendAsync("AnomalyDetected", notification);

    public Task NotifyIncidentCreatedAsync(int tenantId, IncidentNotification notification) =>
        _hubContext.Clients.Group(OpsDashHub.TenantGroupName(tenantId)).SendAsync("IncidentCreated", notification);

    public Task NotifyIncidentUpdatedAsync(int tenantId, IncidentNotification notification) =>
        _hubContext.Clients.Group(OpsDashHub.TenantGroupName(tenantId)).SendAsync("IncidentUpdated", notification);

    public Task NotifyHealthScoreUpdatedAsync(int tenantId, HealthScoreNotification notification) =>
        _hubContext.Clients.Group(OpsDashHub.TenantGroupName(tenantId)).SendAsync("HealthScoreUpdated", notification);

    public Task NotifyAlertTriggeredAsync(int tenantId, AlertNotification notification) =>
        _hubContext.Clients.Group(OpsDashHub.TenantGroupName(tenantId)).SendAsync("AlertTriggered", notification);
}
