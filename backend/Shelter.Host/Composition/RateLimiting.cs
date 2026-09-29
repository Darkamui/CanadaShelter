using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Shelter.BuildingBlocks.Authorization;

namespace Shelter.Host.Composition;

/// <summary>
/// ASP.NET Core's in-memory rate limiter (ADR 0020). Endpoints opt in with <c>RequireRateLimiting</c>; the rest are
/// not limited. Counters live in this process: several instances each allow the full limit.
/// </summary>
internal static class RateLimiting
{
    /// <summary>Requests per window and client IP for <see cref="RateLimitPolicies.Anonymous"/>. Default 20.</summary>
    public const string AnonymousPermitLimitKey = "RateLimiting:Anonymous:PermitLimit";

    /// <summary>Window length for <see cref="RateLimitPolicies.Anonymous"/>. Default one minute.</summary>
    public const string AnonymousWindowKey = "RateLimiting:Anonymous:Window";

    public static IServiceCollection AddShelterRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue(AnonymousPermitLimitKey, 20);
        var window = configuration.GetValue(AnonymousWindowKey, TimeSpan.FromMinutes(1));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // The IP is the connection's; behind a proxy it needs forwarded headers configured, or every client shares one window.
            options.AddPolicy(RateLimitPolicies.Anonymous, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0 }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests. Try again later." },
                });
            };
        });

        return services;
    }
}
