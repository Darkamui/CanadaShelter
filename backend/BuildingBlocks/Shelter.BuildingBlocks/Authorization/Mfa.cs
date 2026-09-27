using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Shelter.BuildingBlocks.Authorization;

/// <summary>
/// Whether MFA is enforced for the roles that require it (ADR 0018). Always on, except when
/// <see cref="DevelopmentBypassKey"/> is true <b>and</b> the environment is Development; the flag has no effect
/// anywhere else. Singleton, decided once at startup.
/// </summary>
public sealed class MfaEnforcement
{
    /// <summary>Configuration flag that skips enforcement in Development only.</summary>
    public const string DevelopmentBypassKey = "Auth:Mfa:DevelopmentBypass";

    private MfaEnforcement(bool enforced) => Enforced = enforced;

    /// <summary>False only under the Development bypass.</summary>
    public bool Enforced { get; }

    /// <summary>Enforced unless the bypass flag is set in the Development environment.</summary>
    public static MfaEnforcement From(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        return new MfaEnforcement(!(environment.IsDevelopment() && configuration.GetValue(DevelopmentBypassKey, false)));
    }
}

/// <summary>Whether this request's session must enroll in MFA before reaching tenant endpoints. Scoped.</summary>
public interface IMfaContext
{
    /// <summary>
    /// True when the account must use MFA (administrator of the active organization, or platform operator) and this
    /// session did not sign in with it. Such a session reaches only the <c>RequireSession</c> endpoints (enrollment).
    /// </summary>
    bool EnrollmentRequired { get; }
}

/// <summary>Set once per request by the tenant resolver.</summary>
public sealed class MfaContext : IMfaContext
{
    /// <inheritdoc />
    public bool EnrollmentRequired { get; private set; }

    /// <summary>Records whether this session must enroll first.</summary>
    public void Set(bool enrollmentRequired) => EnrollmentRequired = enrollmentRequired;
}

/// <summary>Met unless the session must enroll in MFA first (<see cref="IMfaContext.EnrollmentRequired"/>).</summary>
public sealed class MfaRequirement : IAuthorizationRequirement
{
    /// <summary>The shared instance.</summary>
    public static MfaRequirement Instance { get; } = new();
}

/// <summary>Evaluates <see cref="MfaRequirement"/> against the request's <see cref="IMfaContext"/>. Scoped.</summary>
public sealed class MfaAuthorizationHandler(IMfaContext mfaContext) : AuthorizationHandler<MfaRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MfaRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!mfaContext.EnrollmentRequired)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
