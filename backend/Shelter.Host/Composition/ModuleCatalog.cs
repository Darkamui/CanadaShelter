using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Animals;
using Shelter.Modules.Engagement;
using Shelter.Modules.Medical;
using Shelter.Modules.Movements;
using Shelter.Modules.Municipal;
using Shelter.Modules.Operations;
using Shelter.Modules.People;
using Shelter.Modules.Platform;
using Shelter.Modules.Reporting;

namespace Shelter.Host.Composition;

/// <summary>Every module, listed explicitly (no assembly scanning). The Host only calls <see cref="IModule"/> members.</summary>
internal static class ModuleCatalog
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new PlatformModule(),
        new AnimalsModule(),
        new PeopleModule(),
        new MovementsModule(),
        new MedicalModule(),
        new OperationsModule(),
        new EngagementModule(),
        new MunicipalModule(),
        new ReportingModule(),
    ];

    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)
    {
        foreach (var module in All)
        {
            services.AddSingleton(module);
            module.AddServices(services, configuration);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder endpoints)
    {
        // Resolved from DI (registered above) so integration tests can add a test-only module.
        foreach (var module in endpoints.ServiceProvider.GetServices<IModule>())
        {
            // Every module endpoint runs in one unit of work when the request has a tenant (ADR 0004).
            var group = endpoints.MapGroup($"/api/{module.RoutePrefix}")
                .WithTags(module.RoutePrefix)
                .AddEndpointFilter<UnitOfWorkEndpointFilter>();
            module.MapEndpoints(group);
        }

        return endpoints;
    }
}
