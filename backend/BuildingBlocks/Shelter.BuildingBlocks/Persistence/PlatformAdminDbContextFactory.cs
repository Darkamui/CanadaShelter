using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Persistence;

/// <summary>
/// Creates <see cref="ShelterDbContext"/>s on the <c>PlatformAdmin</c> connection (role
/// <c>shelter_platform_admin</c>) for cross-tenant platform operations such as provisioning (architecture §8.3).
/// Registered only by the Platform module and never injected elsewhere (architecture test).
/// Contexts have no tenant: queries over tenant-owned entities must opt out of the tenant filter explicitly with
/// <c>IgnoreQueryFilters([ShelterDbContext.TenantFilter])</c>, and inserts must set <c>TenantId</c> themselves.
/// </summary>
public sealed class PlatformAdminDbContextFactory
{
    /// <summary>Platform-admin connection string name.</summary>
    public const string ConnectionName = "PlatformAdmin";

    private readonly IConfiguration _configuration;
    private readonly IReadOnlyList<IModelContributor> _contributors;
    private readonly Lazy<DbContextOptions<ShelterDbContext>> _options;

    /// <summary>Creates the factory; the connection string is read on first use.</summary>
    public PlatformAdminDbContextFactory(IConfiguration configuration, IEnumerable<IModelContributor> contributors)
    {
        _configuration = configuration;
        _contributors = [.. contributors];
        _options = new(CreateOptions);
    }

    /// <summary>A new platform-admin context; the caller disposes it.</summary>
    public ShelterDbContext CreateDbContext() =>
        new(_options.Value, new TenantContext(), _contributors) { IsPlatformAdmin = true };

    private DbContextOptions<ShelterDbContext> CreateOptions()
    {
        var connectionString = _configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionName}' is not configured.");

        return (DbContextOptions<ShelterDbContext>)PersistenceServiceCollectionExtensions
            .Configure(new DbContextOptionsBuilder<ShelterDbContext>(), connectionString)
            .Options;
    }
}

/// <summary>Registration of the platform-admin persistence path.</summary>
public static class PlatformAdminServiceCollectionExtensions
{
    /// <summary>Adds <see cref="PlatformAdminDbContextFactory"/>. Called by the Platform module only.</summary>
    public static IServiceCollection AddShelterPlatformAdminPersistence(this IServiceCollection services)
    {
        services.AddSingleton<PlatformAdminDbContextFactory>();
        return services;
    }
}
