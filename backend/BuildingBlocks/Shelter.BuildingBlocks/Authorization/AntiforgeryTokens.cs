using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Shelter.BuildingBlocks.Authorization;

/// <summary>Issues the readable request-token cookie of the antiforgery double-submit (ADR 0018).</summary>
public static class AntiforgeryTokens
{
    /// <summary>
    /// Stores a fresh antiforgery cookie token and writes the matching request token to the readable
    /// <see cref="AntiforgeryEndpointFilter.RequestTokenCookie"/>. Tokens are bound to <see cref="HttpContext.User"/>,
    /// so call this again after the user changes (login, logout), with <c>User</c> already set to the new principal.
    /// </summary>
    public static void Issue(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        context.Response.Cookies.Append(AntiforgeryEndpointFilter.RequestTokenCookie, tokens.RequestToken!, new CookieOptions
        {
            // Read by the SPA's script and echoed in the header; worthless without the HttpOnly cookie token.
            HttpOnly = false,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
        });
    }
}
