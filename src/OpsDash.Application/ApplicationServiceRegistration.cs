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

        services.AddAutoMapper(typeof(UserMappingProfile).Assembly);
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMetricService, MetricService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IHealthScoreService, HealthScoreService>();
        services.AddScoped<IAnomalyService, AnomalyService>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IAnomalyDetectionService, AnomalyDetectionService>();

        return services;
    }
}
