namespace Shelter.BuildingBlocks.Authorization;

/// <summary>Rate limiter policy names; the limiters are configured by the Host (ADR 0020).</summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Anonymous endpoints that take a password, code or secret (sign-in, reset, invitations). One fixed window per
    /// client IP, shared by all of them; over the limit answers <c>429</c>.
    /// </summary>
    public const string Anonymous = "anonymous";
}
