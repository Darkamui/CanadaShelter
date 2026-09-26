using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Modules;

namespace Shelter.IntegrationTests.Infrastructure;

/// <summary>
/// The real Host pipeline against a given database. Runs in a non-Development environment so
/// responses match production (no developer exception page).
/// </summary>
public sealed class ShelterApiFactory(string appConnectionString, params IModule[] extraModules)
    : WebApplicationFactory<Program>
{
    /// <summary>A connection string that fails fast: nothing listens on port 1.</summary>
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=shelter;Username=shelter_app;Password=x;Timeout=2";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("ConnectionStrings:App", appConnectionString);
        builder.ConfigureTestServices(services =>
        {
            foreach (var module in extraModules)
            {
                services.AddSingleton(module);
            }
        });
    }
}
