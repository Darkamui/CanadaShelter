using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Modules;

namespace Shelter.Modules.Operations;

/// <summary>Registration entry point of the Operations module.</summary>
public sealed class OperationsModule : IModule
{
    /// <inheritdoc />
    public string RoutePrefix => "operations";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
