using System.Diagnostics;
using Shelter.Host.Middleware;

namespace Shelter.Host.Composition;

/// <summary>RFC 7807 responses for every error, carrying correlation and trace IDs (no exception details outside Development).</summary>
internal static class ProblemDetailsSetup
{
    public static IServiceCollection AddShelterProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var http = context.HttpContext;
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;

            if (CorrelationIdMiddleware.TryGet(http, out var correlationId))
            {
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
            }
        });
}
