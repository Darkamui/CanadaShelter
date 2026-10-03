using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Operations.Authorization;
using Shelter.Modules.Operations.Contracts;
using Shelter.Modules.Operations.Features.Locations;
using Shelter.Modules.Operations.Persistence;

namespace Shelter.Modules.Operations;

/// <summary>Registration entry point of the Operations module.</summary>
public sealed class OperationsModule : IModule
{
    /// <inheritdoc />
    public string RoutePrefix => "operations";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelContributor, OperationsModelContributor>();
        services.AddPermissions(OperationsPermissions.All);
        services.AddScoped<ILocationDirectory, LocationDirectory>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        LocationEndpoints.Map(endpoints);
    }
}
