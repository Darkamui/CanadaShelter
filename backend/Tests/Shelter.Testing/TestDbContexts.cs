using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Testing;

/// <summary>Builds <see cref="ShelterDbContext"/>s configured exactly as the Host does, for a chosen tenant.</summary>
public static class TestDbContexts
{
    /// <summary>A context for <paramref name="tenantId"/> (or none) composed from <paramref name="contributors"/>.</summary>
    public static ShelterDbContext Create(string connectionString, Guid? tenantId, params IModelContributor[] contributors) =>
        Create(connectionString, tenantId, new AuditContext(), contributors);

    /// <summary>
    /// A context for <paramref name="tenantId"/> (or none) that stamps <paramref name="auditContext"/> on its audit
    /// events.
    /// </summary>
    public static ShelterDbContext Create(
        string connectionString, Guid? tenantId, AuditContext auditContext, params IModelContributor[] contributors)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.Set(id);
        }

        var options = PersistenceServiceCollectionExtensions
            .Configure(new DbContextOptionsBuilder<ShelterDbContext>(), connectionString)
            .Options;

        return new ShelterDbContext((DbContextOptions<ShelterDbContext>)options, tenantContext, contributors, auditContext);
    }

    /// <summary>A platform-admin factory on <paramref name="connectionString"/>, as the Platform module registers it.</summary>
    public static PlatformAdminDbContextFactory CreatePlatformAdminFactory(string connectionString, params IModelContributor[] contributors)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{PlatformAdminDbContextFactory.ConnectionName}"] = connectionString,
            })
            .Build();

        return new PlatformAdminDbContextFactory(configuration, contributors);
    }

    /// <summary>Runs <paramref name="work"/> in one committed unit of work on <paramref name="db"/>, as an endpoint does.</summary>
    public static Task<T> InUnitOfWorkAsync<T>(this ShelterDbContext db, Func<Task<T>> work) =>
        new UnitOfWork(db).ExecuteAsync(_ => work(), static _ => true);
}
