using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shelter.BuildingBlocks.Modules;

/// <summary>
/// The single registration entry point of a module. The Host lists every module explicitly
/// (<c>Shelter.Host/Composition/ModuleCatalog.cs</c>) and only ever calls these members.
/// </summary>
public interface IModule
{
    /// <summary>Route segment under <c>/api</c>, e.g. <c>platform</c> → <c>/api/platform</c>. Also the OpenAPI tag.</summary>
    string RoutePrefix { get; }

    /// <summary>Registers the module's services.</summary>
    void AddServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps the module's endpoints onto its <c>/api/{RoutePrefix}</c> route group.</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
