using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Shelter.Modules.Platform.Features.Ping;

/// <summary><c>GET /api/platform/ping</c>: proves the request pipeline end to end.</summary>
internal static class PingEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/ping", Handle)
            .WithName("GetPlatformPing")
            // Anonymous until identity exists (M2). M2 sets an authorization FallbackPolicy so
            // default-deny is structural and this endpoint must then declare a permission.
            .AllowAnonymous();

    internal static Ok<PingResponse> Handle() => TypedResults.Ok(new PingResponse("ok"));
}

/// <summary>Ping result.</summary>
/// <param name="Status">Always <c>ok</c> when the pipeline works.</param>
internal sealed record PingResponse(string Status);
