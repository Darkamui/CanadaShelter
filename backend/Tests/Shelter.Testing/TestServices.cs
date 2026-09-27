using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Testing;

/// <summary>
/// Persistence and auditing registered as the Host registers them, without the web host: for tests that need the
/// scoped services (<c>IAuditReader</c>, <c>ISubjectShredder</c>) rather than a bare context.
/// </summary>
public static class TestServices
{
    /// <summary>A provider on <paramref name="connectionString"/> composed from <paramref name="contributors"/>.</summary>
    public static ServiceProvider Create(string connectionString, params IModelContributor[] contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{PersistenceServiceCollectionExtensions.AppConnectionName}"] = connectionString,
                [PersistenceServiceCollectionExtensions.MasterKeyKey] = TestKeys.MasterKeyBase64,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        foreach (var contributor in contributors)
        {
            services.AddSingleton(contributor);
        }

        services.AddShelterPermissions();
        services.AddShelterPersistence();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
