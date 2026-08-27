using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using StitchERP.Application.Programs;
using StitchERP.Application.Inventory;
using StitchERP.Application.Procurement;
using StitchERP.Application.Sales;
using StitchERP.Application.Governance;
using StitchERP.Application.Identity;

namespace StitchERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IProgramBomService, ProgramBomService>();
        services.AddSingleton<IInventoryService, InventoryService>();
        services.AddSingleton<IP2PService, P2PService>();
        services.AddSingleton<IO2CService, O2CService>();
        services.AddSingleton<IGovernanceService, GovernanceService>();
        services.AddScoped<IUserStore, InMemoryUserStore>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddSingleton<ISessionTokenService>(_ =>
        {
            var secret = configuration["JWT_SECRET"] ?? configuration["Jwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret) ||
                (configuration["ASPNETCORE_ENVIRONMENT"] == "Production" && secret == "your-secret-key-change-in-production"))
                throw new InvalidOperationException("A strong JWT secret must be configured in JWT_SECRET.");
            return new SessionTokenService(secret);
        });
        return services;
    }
}
