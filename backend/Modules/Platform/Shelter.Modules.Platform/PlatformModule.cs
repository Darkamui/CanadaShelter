using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Platform.Features.Ping;
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
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        PingEndpoint.Map(endpoints);
    }
}
