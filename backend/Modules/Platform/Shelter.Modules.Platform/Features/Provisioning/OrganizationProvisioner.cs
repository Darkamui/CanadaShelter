using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Features.Provisioning;

/// <summary>
/// Creates organizations. Runs on the platform-admin connection: the runtime role cannot write
/// <c>platform.organization</c> (architecture §8.3). No endpoint in M1; M2 exposes it behind a platform permission.
/// </summary>
internal sealed class OrganizationProvisioner(PlatformAdminDbContextFactory contextFactory, TimeProvider timeProvider)
{
    public async Task<Guid> ProvisionAsync(string name, string slug, CancellationToken cancellationToken = default)
    {
        var organization = new Organization(name, slug, timeProvider.GetUtcNow());

        await using var db = contextFactory.CreateDbContext();
        db.Set<Organization>().Add(organization);
        await db.SaveChangesAsync(cancellationToken);

        return organization.Id;
    }
}
