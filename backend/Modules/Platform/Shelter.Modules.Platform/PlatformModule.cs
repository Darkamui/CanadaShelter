using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Modules;
using Shelter.Modules.Platform.Features.Ping;

namespace Shelter.Modules.Platform;

/// <summary>Registration entry point of the Platform module.</summary>
public sealed class PlatformModule : IModule
{
    /// <inheritdoc />
    public string RoutePrefix => "platform";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        PingEndpoint.Map(endpoints);
    }
}
