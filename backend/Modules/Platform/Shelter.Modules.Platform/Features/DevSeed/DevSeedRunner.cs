using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.DevSeed;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Domain;
using Shelter.Modules.Platform.Features.Provisioning;

namespace Shelter.Modules.Platform.Features.DevSeed;

/// <summary>
/// Runs every module's <see cref="IDevSeedContributor"/> for one organization (ADR 0023): the caller's current one, or
/// a new one provisioned for the run with the caller as its administrator. The contributors run in their own scope and
/// one transaction, as that organization, with administrator permissions and the caller as the signed-in user and
/// audit actor. The seed data is created all or nothing; a new organization is provisioned first, on its own
/// connection, and stays (empty) if the seed then fails. Singleton.
/// </summary>
internal sealed class DevSeedRunner(IServiceScopeFactory scopeFactory, OrganizationProvisioner provisioner, TimeProvider timeProvider)
{
    public async Task<DevSeedResponse> RunAsync(
        Guid userId, Guid? currentOrganizationId, DevSeedOptions options, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        string? organizationName = null;
        Guid organizationId;
        if (options.NewOrganization)
        {
            organizationName = options.OrganizationName
                ?? string.Create(CultureInfo.InvariantCulture, $"Refuge démo {now:yyyy-MM-dd HH:mm:ss}");
            var slug = string.Create(CultureInfo.InvariantCulture, $"demo-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}")[..32];
            organizationId = await provisioner.ProvisionAsync(organizationName, slug, cancellationToken);
        }
        else
        {
            organizationId = currentOrganizationId
                ?? throw new InvalidOperationException("Seeding the current organization needs one.");
        }

        var context = new DevSeedContext(options.Factor, options.Seed, now);
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<TenantContext>().Set(organizationId);
        services.GetRequiredService<UserContext>().Set(userId);
        services.GetRequiredService<PermissionContext>().Set(
            services.GetRequiredService<PermissionCatalog>().PermissionsFor([SystemRoles.Administrator]));
        services.GetRequiredService<AuditContext>().Set(AuditActorType.User, userId, AuditSources.DevSeed, null);

        var contributors = services.GetServices<IDevSeedContributor>().OrderBy(c => c.Order).ToList();
        await services.GetRequiredService<UnitOfWork>().ExecuteAsync(
            async ct =>
            {
                if (options.NewOrganization)
                {
                    await AddAdministratorAsync(services.GetRequiredService<ShelterDbContext>(), userId, now, ct);
                }

                foreach (var contributor in contributors)
                {
                    await contributor.SeedAsync(context, ct);
                }
            },
            cancellationToken);

        return new DevSeedResponse(organizationId, organizationName, options.Factor, options.Seed, context.Counts);
    }

    private static async Task AddAdministratorAsync(ShelterDbContext db, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!await db.Set<StaffMembership>().AnyAsync(m => m.UserId == userId, cancellationToken))
        {
            db.Set<StaffMembership>().Add(new StaffMembership(userId, [SystemRoles.Administrator], now));
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>A validated seed request.</summary>
internal sealed record DevSeedOptions(int Factor, int Seed, bool NewOrganization, string? OrganizationName);
