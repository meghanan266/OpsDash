namespace OpsDash.Application.Interfaces;

public interface IPredictiveAlertService
{
    Task EvaluateAlertsAsync(long metricId);

    Task EvaluatePredictiveAlertsAsync(string metricName);
}
