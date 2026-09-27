using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Animals.Features.ReferenceData;
using Shelter.Modules.Animals.Persistence;

namespace Shelter.Modules.Animals;

/// <summary>Registration entry point of the Animals module.</summary>
public sealed class AnimalsModule : IModule
{
    /// <inheritdoc />
    public string RoutePrefix => "animals";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelContributor, AnimalsModelContributor>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ListSpeciesEndpoint.Map(endpoints);
    }
}
