using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpsDash.Application.Configuration;
using OpsDash.Application.Interfaces;
using OpsDash.Application.Mappings;
using OpsDash.Application.Services;

namespace OpsDash.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AnomalyDetectionSettings>(configuration.GetSection("AnomalyDetection"));
        services.Configure<ForecastSettings>(configuration.GetSection("Forecasting"));

        services.AddAutoMapper(typeof(UserMappingProfile).Assembly);
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMetricService, MetricService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IHealthScoreService, HealthScoreService>();
        services.AddScoped<IAnomalyService, AnomalyService>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<ICorrelationService, CorrelationService>();
        services.AddScoped<IAnomalyDetectionService, AnomalyDetectionService>();
        services.AddScoped<IForecastService, ForecastService>();
        services.AddScoped<IPredictiveAlertService, PredictiveAlertService>();
        services.AddScoped<IHealthScoreComputeService, HealthScoreComputeService>();
        services.AddScoped<IIncidentAutoGroupService, IncidentAutoGroupService>();
        services.AddScoped<IDashboardSummaryQuery, DashboardSummaryQuery>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
