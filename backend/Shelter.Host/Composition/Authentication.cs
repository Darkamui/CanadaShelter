using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Shelter.BuildingBlocks.Authorization;

namespace Shelter.Host.Composition;

/// <summary>
/// Cookie authentication, authorization policies and antiforgery (ADR 0007, 0018). Accounts and the Identity
/// services are the Platform module's; this is the HTTP side.
/// </summary>
internal static class Authentication
{
    /// <summary>Session lifetime, renewed while the user is active.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    /// <summary>How long a password-verified login may wait for its MFA code.</summary>
    public static readonly TimeSpan TwoFactorLifetime = TimeSpan.FromMinutes(5);

    public static IServiceCollection AddShelterAuthentication(this IServiceCollection services, IHostEnvironment environment)
    {
        // Development serves plain HTTP on localhost (Vite proxy), where browsers refuse __Host- cookies, which must
        // be Secure. Every other environment gets the prefix and Secure unconditionally.
        var strict = !environment.IsDevelopment();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                ConfigureCookie(options, strict, "shelter-session");
                options.ExpireTimeSpan = SessionLifetime;
                options.SlidingExpiration = true;

                // A password or MFA change rotates the security stamp and ends every other session.
                options.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;
            })
            .AddCookie(IdentityConstants.TwoFactorUserIdScheme, options =>
            {
                ConfigureCookie(options, strict, "shelter-mfa");
                options.ExpireTimeSpan = TwoFactorLifetime;
                options.SlidingExpiration = false;
            })
            // Never signed in (no "remember this browser"), but the security stamp validator and sign-out expect
            // the scheme to exist.
            .AddCookie(IdentityConstants.TwoFactorRememberMeScheme, options => ConfigureCookie(options, strict, "shelter-mfa-remember"));

        // Default deny: an endpoint that declares nothing still needs a member of the active organization, and the
        // endpoint coverage test fails on it.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().Tenant().Build())
            .AddPolicy(AuthorizationPolicies.Session, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(AuthorizationPolicies.PlatformOperator, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AuthorizationPolicies.PlatformOperatorClaim, "true")
                .RequireClaim(AuthorizationPolicies.AuthenticationMethodClaim, "mfa"));
        services.AddScoped<IAuthorizationHandler, TenantAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddShelterPermissions();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryEndpointFilter.HeaderName;
            options.Cookie.Name = CookieName(strict, "shelter-af");
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = strict ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        });

        return services;
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options, bool strict, string name)
    {
        options.Cookie.Name = CookieName(strict, name);
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = strict ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        options.Cookie.Path = "/";

        // An API never redirects to a login page: 401/403, turned into ProblemDetails by the status code pages.
        options.Events.OnRedirectToLogin = context => Status(context.Response, StatusCodes.Status401Unauthorized);
        options.Events.OnRedirectToAccessDenied = context => Status(context.Response, StatusCodes.Status403Forbidden);
        options.Events.OnRedirectToLogout = context => Status(context.Response, StatusCodes.Status204NoContent);
        options.Events.OnRedirectToReturnUrl = context => Status(context.Response, StatusCodes.Status204NoContent);
    }

    private static string CookieName(bool strict, string name) => strict ? "__Host-" + name : name;

    private static Task Status(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
