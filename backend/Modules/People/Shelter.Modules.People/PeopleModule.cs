using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.People.Authorization;
using Shelter.Modules.People.Contracts;
using Shelter.Modules.People.Features.People;
using Shelter.Modules.People.Persistence;

namespace Shelter.Modules.People;

/// <summary>Registration entry point of the People module.</summary>
public sealed class PeopleModule : IModule
{
    /// <inheritdoc />
    public string RoutePrefix => "people";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelContributor, PeopleModelContributor>();
        services.AddPermissions(PeoplePermissions.All);
        services.AddScoped<IPersonDirectory, PersonDirectory>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        PeopleEndpoints.Map(endpoints);
    }
}
