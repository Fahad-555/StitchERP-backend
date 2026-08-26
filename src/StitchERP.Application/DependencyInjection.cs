using Microsoft.Extensions.DependencyInjection;
using StitchERP.Application.Programs;
using StitchERP.Application.Inventory;
using StitchERP.Application.Procurement;
using StitchERP.Application.Sales;
using StitchERP.Application.Governance;
using StitchERP.Application.Identity;

namespace StitchERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IProgramBomService, ProgramBomService>();
        services.AddSingleton<IInventoryService, InventoryService>();
        services.AddSingleton<IP2PService, P2PService>();
        services.AddSingleton<IO2CService, O2CService>();
        services.AddSingleton<IGovernanceService, GovernanceService>();
        services.AddSingleton<IUserStore, InMemoryUserStore>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IUserAdministrationService, UserAdministrationService>();
        return services;
    }
}
