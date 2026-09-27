using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Platform.Domain;
using Shelter.Modules.Platform.Features.Provisioning;

namespace Shelter.Modules.Platform.Features.DevSeed;

/// <summary>
/// Development-only seed: a demo organization and an administrator account, so a fresh local database can be
/// signed into. Does nothing outside Development or without <c>DevSeed:Enabled</c>. Idempotent.
/// </summary>
internal sealed partial class DevelopmentSeed(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    IConfiguration configuration,
    ILogger<DevelopmentSeed> logger) : IHostedService
{
    public const string DemoOrganizationName = "Refuge démo";
    public const string DemoOrganizationSlug = "refuge-demo";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = configuration.GetSection("DevSeed");
        if (!environment.IsDevelopment() || !options.GetValue<bool>("Enabled"))
        {
            return;
        }

        try
        {
            await SeedAsync(options, cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or Npgsql.NpgsqlException or InvalidOperationException)
        {
            // Typically migrations not applied yet; the app still starts.
            LogSeedFailed(logger, exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAsync(IConfiguration options, CancellationToken cancellationToken)
    {
        var email = options["AdminEmail"] ?? throw new InvalidOperationException("DevSeed:AdminEmail is not configured.");
        var password = options["AdminPassword"] ?? throw new InvalidOperationException("DevSeed:AdminPassword is not configured.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;

        await using (var admin = services.GetRequiredService<PlatformAdminDbContextFactory>().CreateDbContext())
        {
            if (!await admin.Set<Organization>().AnyAsync(o => o.Slug == DemoOrganizationSlug, cancellationToken))
            {
                await services.GetRequiredService<OrganizationProvisioner>().ProvisionAsync(DemoOrganizationName, DemoOrganizationSlug, cancellationToken);
                LogCreated(logger, "organization");
            }
        }

        var userManager = services.GetRequiredService<UserManager<UserAccount>>();
        if (await userManager.FindByEmailAsync(email) is null)
        {
            var timeProvider = services.GetRequiredService<TimeProvider>();
            var user = new UserAccount(email, "Admin démo", Languages.French, timeProvider.GetUtcNow()) { EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Dev seed administrator could not be created: " + string.Join(", ", result.Errors.Select(e => e.Code)));
            }

            LogCreated(logger, "administrator");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Dev seed created the demo {Item}.")]
    private static partial void LogCreated(ILogger logger, string item);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dev seed skipped; apply migrations and restart.")]
    private static partial void LogSeedFailed(ILogger logger, Exception exception);
}
