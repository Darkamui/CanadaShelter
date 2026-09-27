using Microsoft.AspNetCore.Builder;

namespace Shelter.BuildingBlocks.Authorization;

/// <summary>Named authorization policies shared by the Host and the modules (ADR 0018).</summary>
public static class AuthorizationPolicies
{
    /// <summary>A signed-in user; no organization or permission needed (session, logout, organization switch, MFA enrollment).</summary>
    public const string Session = "Session";

    /// <summary>A signed-in user with an active membership in the session's active organization (<see cref="TenantRequirement"/>).</summary>
    public const string Tenant = "Tenant";
}

/// <summary>Marks an endpoint as reachable by any signed-in user, without an organization or permission.</summary>
public sealed class SessionEndpointMetadata
{
    /// <summary>The shared instance.</summary>
    public static SessionEndpointMetadata Instance { get; } = new();
}

/// <summary>How endpoints declare who may call them. Every module endpoint uses exactly one of these or is allowlisted anonymous.</summary>
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
    /// A signed-in user acting in an organization where they hold an active membership. The floor for organization
    /// data until M2-3 adds permissions on top.
    /// </summary>
    public static TBuilder RequireTenant<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RequireAuthorization(AuthorizationPolicies.Tenant);
    }
}
