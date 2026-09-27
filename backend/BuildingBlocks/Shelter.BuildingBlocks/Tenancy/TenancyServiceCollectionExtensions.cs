using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Shelter.BuildingBlocks.Tenancy;

/// <summary>Registers the tenant context and the request tenant resolver.</summary>
public static class TenancyServiceCollectionExtensions
{
    /// <summary>
    /// Adds the scoped <see cref="TenantContext"/>. The header resolver is registered only in Development;
    /// every other environment resolves no tenant until M2 replaces the resolver.
    /// </summary>
    public static IServiceCollection AddShelterTenancy(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        if (environment.IsDevelopment())
        {
            services.AddSingleton<ITenantResolver, DevelopmentHeaderTenantResolver>();
        }
        else
        {
            services.AddSingleton<ITenantResolver, NoTenantResolver>();
        }

        return services;
    }
}
