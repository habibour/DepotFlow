using DepotFlow.Application;
using DepotFlow.Application.Auth;
using DepotFlow.Infrastructure.Identity;
using DepotFlow.Infrastructure.Persistence;
using DepotFlow.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DepotFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Read lazily, when the context is first created, so configuration added later (for example by tests) is honoured.
        services.AddDbContext<DepotFlowDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
            options.UseSqlServer(connectionString);
        });
        services.AddScoped<IDepotFlowDbContext>(sp => sp.GetRequiredService<DepotFlowDbContext>());

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<DepotFlowDbContext>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.Key.Length >= 32, "Jwt:Key must be at least 32 characters.")
            .ValidateOnStart();

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
