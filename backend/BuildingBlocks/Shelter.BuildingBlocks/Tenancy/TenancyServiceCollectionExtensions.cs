using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Shelter.BuildingBlocks.Tenancy;

/// <summary>Registers the tenant and user contexts and the request tenant resolver.</summary>
public static class TenancyServiceCollectionExtensions
{
    /// <summary>
    /// Adds the scoped <see cref="TenantContext"/> and <see cref="UserContext"/>, and <see cref="NoTenantResolver"/>
    /// unless a resolver is already registered. The Platform module replaces it with the membership resolver.
    /// </summary>
    public static IServiceCollection AddShelterTenancy(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<UserContext>();
        services.AddScoped<IUserContext>(sp => sp.GetRequiredService<UserContext>());
        services.TryAddSingleton<ITenantResolver, NoTenantResolver>();
        return services;
    }
}
