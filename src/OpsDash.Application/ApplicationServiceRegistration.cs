using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OpsDash.Application.Interfaces;
using OpsDash.Application.Mappings;
using OpsDash.Application.Services;

namespace OpsDash.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapper(typeof(UserMappingProfile).Assembly);
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
