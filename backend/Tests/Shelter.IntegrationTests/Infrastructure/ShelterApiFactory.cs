using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shelter.BuildingBlocks.Communications;
using Shelter.BuildingBlocks.Modules;
using Shelter.Testing;

namespace Shelter.IntegrationTests.Infrastructure;

/// <summary>
/// The real Host pipeline against a given database. Runs in a non-Development environment by default so
/// responses match production (no developer exception page).
/// </summary>
public sealed class ShelterApiFactory(string appConnectionString, params IModule[] extraModules)
    : WebApplicationFactory<Program>
{
    /// <summary>A connection string that fails fast: nothing listens on port 1.</summary>
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=shelter;Username=shelter_app;Password=x;Timeout=2";

    /// <summary>Host environment name; <c>Test</c> unless a test needs Development-only behaviour.</summary>
    public string EnvironmentName { get; init; } = "Test";

    /// <summary>Runs the Hangfire server. Off by default: only job tests process jobs.</summary>
    public bool JobServerEnabled { get; init; }

    /// <summary>Extra configuration values, applied after the defaults above.</summary>
    public IReadOnlyDictionary<string, string?> Settings { get; init; } = new Dictionary<string, string?>();

    /// <summary>Base URL of links in emails.</summary>
    public const string PublicBaseUrl = "https://app.test";

    /// <summary>Emails the app sent (none leave the process).</summary>
    public CapturingEmailSender Emails { get; } = new();

    /// <summary>Extra service registrations (e.g. test-only jobs), applied after the Host's.</summary>
    public Action<IServiceCollection>? ConfigureServices { get; init; }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:App", appConnectionString);

        // Never fall through to the local-dev PlatformAdmin string in appsettings.Development.json.
        builder.UseSetting("ConnectionStrings:PlatformAdmin", UnreachableDatabase);
        builder.UseSetting("Audit:MasterKey", TestKeys.MasterKeyBase64);
        builder.UseSetting("Jobs:ServerEnabled", JobServerEnabled ? "true" : "false");
        builder.UseSetting("Jobs:QueuePollInterval", "00:00:00.500");
        builder.UseSetting("App:PublicBaseUrl", PublicBaseUrl);
        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            foreach (var module in extraModules)
            {
                services.AddSingleton(module);
            }

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.AddTestAuthentication();
            ConfigureServices?.Invoke(services);
        });
    }
}
