using OpsDash.Application.DTOs.Notifications;

namespace OpsDash.Application.Interfaces;

public interface IRealtimeNotificationService
{
    Task NotifyAnomalyDetectedAsync(int tenantId, AnomalyNotification notification);

    Task NotifyIncidentCreatedAsync(int tenantId, IncidentNotification notification);

    Task NotifyIncidentUpdatedAsync(int tenantId, IncidentNotification notification);

    Task NotifyHealthScoreUpdatedAsync(int tenantId, HealthScoreNotification notification);

    Task NotifyAlertTriggeredAsync(int tenantId, AlertNotification notification);
}
