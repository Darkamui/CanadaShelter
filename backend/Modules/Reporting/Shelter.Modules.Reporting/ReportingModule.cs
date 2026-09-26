using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Modules;

namespace Shelter.Modules.Reporting;

/// <summary>Registration entry point of the Reporting module.</summary>
public sealed class ReportingModule : IModule
{
    /// <inheritdoc />
    public string RoutePrefix => "reporting";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
