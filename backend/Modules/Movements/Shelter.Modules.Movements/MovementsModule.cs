using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Movements.Authorization;
using Shelter.Modules.Movements.Features.ReferenceData;
using Shelter.Modules.Movements.Persistence;

namespace Shelter.Modules.Movements;

/// <summary>Registration entry point of the Movements module.</summary>
public sealed class MovementsModule : IModule
{
    /// <inheritdoc />
    public string RoutePrefix => "movements";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelContributor, MovementsModelContributor>();
        services.AddPermissions(MovementPermissions.All);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ListIntakeReasonsEndpoint.Map(endpoints);
    }
}
