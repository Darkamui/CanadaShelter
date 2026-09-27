using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Testing;

/// <summary>Builds <see cref="ShelterDbContext"/>s configured exactly as the Host does, for a chosen tenant.</summary>
public static class TestDbContexts
{
    /// <summary>A context for <paramref name="tenantId"/> (or none) composed from <paramref name="contributors"/>.</summary>
    public static ShelterDbContext Create(string connectionString, Guid? tenantId, params IModelContributor[] contributors)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.Set(id);
        }

        var options = PersistenceServiceCollectionExtensions
            .Configure(new DbContextOptionsBuilder<ShelterDbContext>(), connectionString)
            .Options;

        return new ShelterDbContext((DbContextOptions<ShelterDbContext>)options, tenantContext, contributors);
    }
}
