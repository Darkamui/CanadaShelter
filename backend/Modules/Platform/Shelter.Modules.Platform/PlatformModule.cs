using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Platform.Authorization;
using Shelter.Modules.Platform.Features.DevSeed;
using Shelter.Modules.Platform.Features.Ping;
using Shelter.Modules.Platform.Features.Provisioning;
using Shelter.Modules.Platform.Features.Session;
using Shelter.Modules.Platform.Features.Staff;
using Shelter.Modules.Platform.Identity;
using Shelter.Modules.Platform.Persistence;

namespace Shelter.Modules.Platform;

/// <summary>Registration entry point of the Platform module.</summary>
public sealed class PlatformModule : IModule
{
    /// <inheritdoc />
    public string RoutePrefix => "platform";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelContributor, PlatformModelContributor>();
        services.AddPermissions(PlatformPermissions.All);
        services.AddShelterPlatformAdminPersistence();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<OrganizationProvisioner>();
        services.AddShelterIdentity(configuration);
        services.AddHostedService<DevelopmentSeed>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        PingEndpoint.Map(endpoints);
        SessionEndpoints.Map(endpoints);
        StaffEndpoints.Map(endpoints);
    }
}
