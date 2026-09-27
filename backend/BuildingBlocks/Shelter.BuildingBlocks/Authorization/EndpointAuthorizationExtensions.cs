using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace Shelter.BuildingBlocks.Authorization;

/// <summary>Named authorization policies and claim types shared by the Host and the modules (ADR 0018).</summary>
public static class AuthorizationPolicies
{
    /// <summary>A signed-in user; no organization or permission needed (session, logout, organization switch, MFA enrollment).</summary>
    public const string Session = "Session";

    /// <summary>A platform operator who signed in with MFA (the job dashboard). Never granted through the API.</summary>
    public const string PlatformOperator = "PlatformOperator";

    /// <summary>Claim present (<c>true</c>) on a platform operator's session; set from <c>user_account.is_platform_operator</c>.</summary>
    public const string PlatformOperatorClaim = "shelter:operator";

    /// <summary>Claim listing how the session signed in (<c>pwd</c>, <c>mfa</c>).</summary>
    public const string AuthenticationMethodClaim = "amr";

    /// <summary>
    /// The floor of every organization endpoint, and the fallback policy of endpoints that declare nothing: a
    /// signed-in user with an active membership in the session's organization.
    /// </summary>
    public static AuthorizationPolicyBuilder Tenant(this AuthorizationPolicyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RequireAuthenticatedUser().AddRequirements(TenantRequirement.Instance);
    }
}

/// <summary>Marks an endpoint as reachable by any signed-in user, without an organization or permission.</summary>
public sealed class SessionEndpointMetadata
{
    /// <summary>The shared instance.</summary>
    public static SessionEndpointMetadata Instance { get; } = new();
}

/// <summary>The permission an endpoint requires; read by the endpoint coverage test.</summary>
/// <param name="Permission">Permission name.</param>
public sealed record PermissionEndpointMetadata(string Permission);

/// <summary>
/// How endpoints declare who may call them. Every endpoint uses exactly one of these or is allowlisted anonymous
/// (enforced by a test). One that declares nothing still gets the fallback policy (<see cref="AuthorizationPolicies.Tenant"/>).
/// </summary>
public static class EndpointAuthorizationExtensions
{
    /// <summary>
    /// Any signed-in user, whether or not an organization is selected. Only for endpoints about the caller's own
    /// session or account; everything touching organization data declares a permission instead.
    /// </summary>
    public static TBuilder RequireSession<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .WithMetadata(SessionEndpointMetadata.Instance)
            .RequireAuthorization(AuthorizationPolicies.Session);
    }

    /// <summary>
    /// A member of the session's organization whose roles grant <paramref name="permission"/>. Denied with 403
    /// otherwise (401 when anonymous).
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return builder
            .WithMetadata(new PermissionEndpointMetadata(permission))
            .RequireAuthorization(policy => policy.Tenant().AddRequirements(new PermissionRequirement(permission)));
    }
}
