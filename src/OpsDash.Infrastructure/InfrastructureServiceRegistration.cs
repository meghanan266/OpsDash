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
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<CurrentTenantService>();
        services.AddScoped<ITenantContextService>(sp => sp.GetRequiredService<CurrentTenantService>());

        return services;
    }
}
