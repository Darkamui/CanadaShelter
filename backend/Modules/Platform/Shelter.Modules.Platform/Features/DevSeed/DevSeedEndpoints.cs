using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Authorization;

namespace Shelter.Modules.Platform.Features.DevSeed;

/// <summary>
/// <c>POST /api/platform/dev/seed</c>: fills an organization with sample data for testing and demos (ADR 0023). Mapped
/// only in Development with <c>DevSeed:Enabled</c>, kept out of the OpenAPI document, and limited to administrators
/// (<see cref="PlatformPermissions.DevSeed"/>). Data is added, never replaced: <c>scripts/dev-reset-data.sh</c> wipes
/// the local database, or <c>newOrganization</c> seeds a fresh organization instead.
/// </summary>
internal static class DevSeedEndpoints
{
    public const int MaxFactor = 200;
    public const int OrganizationNameMaxLength = 100;

    private static readonly Dictionary<string, int> Scales = new(StringComparer.Ordinal)
    {
        ["small"] = 1,
        ["demo"] = 10,
        ["load"] = 100,
    };

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        if (!IsEnabled(endpoints.ServiceProvider))
        {
            return;
        }

        endpoints.MapPost("/dev/seed", Seed)
            .WithName("SeedPlatformDevData")
            .RequirePermission(PlatformPermissions.DevSeed)
            .ExcludeFromDescription();
    }

    /// <summary>Whether the route (and its permission) exist: Development with <c>DevSeed:Enabled</c>.</summary>
    public static bool IsEnabled(IServiceProvider services) =>
        services.GetRequiredService<IHostEnvironment>().IsDevelopment()
        && services.GetRequiredService<IConfiguration>().GetValue<bool>("DevSeed:Enabled");

    internal static async Task<Results<Ok<DevSeedResponse>, ValidationProblem>> Seed(
        SeedDevDataRequest? request,
        ITenantContext tenant,
        IUserContext user,
        DevSeedRunner runner,
        CancellationToken cancellationToken)
    {
        request ??= new SeedDevDataRequest(null, null, null, null, null);
        var errors = new Dictionary<string, string[]>();
        var factor = 10;
        if (request.Factor is { } explicitFactor)
        {
            if (explicitFactor is < 1 or > MaxFactor)
            {
                errors["factor"] = [$"Between 1 and {MaxFactor}."];
            }

            factor = explicitFactor;
        }
        else if (request.Scale is not null && !Scales.TryGetValue(request.Scale, out factor))
        {
            errors["scale"] = ["One of small, demo, load."];
        }

        var name = string.IsNullOrWhiteSpace(request.OrganizationName) ? null : request.OrganizationName.Trim();
        if (name is not null && (request.NewOrganization != true || name.Length > OrganizationNameMaxLength))
        {
            errors["organizationName"] = [$"Only with newOrganization, at most {OrganizationNameMaxLength} characters."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var options = new DevSeedOptions(factor, request.Seed ?? Environment.TickCount, request.NewOrganization == true, name);
        return TypedResults.Ok(await runner.RunAsync(user.UserId!.Value, tenant.TenantId, options, cancellationToken));
    }
}

/// <summary>
/// Seed request; every field is optional. <c>scale</c>: <c>small</c> (factor 1), <c>demo</c> (10, the default) or
/// <c>load</c> (100); <c>factor</c> (1–200) overrides it. <c>seed</c> makes a run reproducible. <c>newOrganization</c>
/// seeds a new organization, named <c>organizationName</c>, with the caller as administrator.
/// </summary>
internal sealed record SeedDevDataRequest(string? Scale, int? Factor, int? Seed, bool? NewOrganization, string? OrganizationName);

/// <summary>
/// The seeded organization (switch to it with <c>POST /api/platform/session/organization/{id}</c>), its name when new,
/// the factor and seed used, and how many of each item were created.
/// </summary>
internal sealed record DevSeedResponse(
    Guid OrganizationId, string? OrganizationName, int Factor, int Seed, IReadOnlyDictionary<string, int> Counts);
