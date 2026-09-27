using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Modules;

namespace Shelter.Modules.Movements;

/// <summary>Registration entry point of the Movements module.</summary>
public sealed class MovementsModule : IModule
{
    /// <inheritdoc />
    public string RoutePrefix => "movements";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
