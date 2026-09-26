using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Modules;

namespace Shelter.Modules.Municipal;

/// <summary>Registration entry point of the Municipal module.</summary>
public sealed class MunicipalModule : IModule
{
    /// <inheritdoc />
    public string RoutePrefix => "municipal";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
