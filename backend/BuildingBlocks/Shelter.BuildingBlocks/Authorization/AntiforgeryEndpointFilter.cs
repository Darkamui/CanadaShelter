using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Shelter.BuildingBlocks.Authorization;

/// <summary>
/// CSRF protection for every state-changing module request, anonymous ones (login) included (ADR 0018): the
/// <c>X-XSRF-TOKEN</c> header must carry the request token that matches the antiforgery cookie and the current
/// user. Safe methods pass through. A missing or wrong token is a 400 ProblemDetails.
/// </summary>
public sealed class AntiforgeryEndpointFilter : IEndpointFilter
{
    /// <summary>The request header carrying the token (double-submit from the readable <see cref="RequestTokenCookie"/>).</summary>
    public const string HeaderName = "X-XSRF-TOKEN";

    /// <summary>The readable cookie the SPA copies into <see cref="HeaderName"/>.</summary>
    public const string RequestTokenCookie = "XSRF-TOKEN";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        if (!IsSafe(http.Request.Method)
            && !await http.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(http))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid or missing antiforgery token.");
        }

        return await next(context);
    }

    private static bool IsSafe(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
}
