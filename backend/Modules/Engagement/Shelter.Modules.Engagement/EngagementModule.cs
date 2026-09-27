using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Modules;

namespace Shelter.Modules.Engagement;

/// <summary>Registration entry point of the Engagement module.</summary>
public sealed class EngagementModule : IModule
{
    /// <inheritdoc />
    public string RoutePrefix => "engagement";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
