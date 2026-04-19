using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpsDash.Application.Interfaces;
using OpsDash.Infrastructure.Data;
using OpsDash.Infrastructure.Services;

namespace OpsDash.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Local dev: in-memory distributed cache. For production, prefer:
        // services.AddStackExchangeRedisCache(o => o.Configuration = configuration["Redis:ConnectionString"]);
        services.AddDistributedMemoryCache();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();
        services.AddScoped<AuditInterceptor>();
        services.AddScoped<ICacheService, CacheService>();

        // IRealtimeNotificationService is registered in OpsDash.API (Program.cs) with SignalR (host owns IHubContext).

        services.AddDbContext<AppDbContext>(
            (sp, options) =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
                options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
            });

        services.AddScoped<CurrentTenantService>();
        services.AddScoped<ITenantContextService>(sp => sp.GetRequiredService<CurrentTenantService>());
        services.AddScoped<ICurrentTenantSetter>(sp => sp.GetRequiredService<CurrentTenantService>());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}
