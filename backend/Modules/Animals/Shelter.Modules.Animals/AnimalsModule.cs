using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Animals.Authorization;
using Shelter.Modules.Animals.Contracts;
using Shelter.Modules.Animals.Features.Animals;
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
        services.AddPermissions(AnimalPermissions.All);
        services.AddScoped<IAnimalCustody, AnimalCustody>();
        services.AddScoped<IAnimalTimeline, AnimalTimeline>();
        services.AddScoped<IAnimalPopulation, AnimalPopulation>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ListSpeciesEndpoint.Map(endpoints);
        AnimalEndpoints.Map(endpoints);
    }
}
