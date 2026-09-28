using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Communications;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Authorization;
using Shelter.Modules.Platform.Communications;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Features.Invitations;

/// <summary>
/// <c>/api/platform/invitations</c>: staff invitations by email (M2-5, ADR 0018).
/// <list type="bullet">
/// <item>Create, list, resend and revoke: <c>platform.staff.manage</c> / <c>platform.staff.read</c>, in the active
/// organization. Every change is an audited update of a tenant-owned row.</item>
/// <item><c>POST /lookup</c> and <c>/accept</c>: anonymous; the secret from the link is the only credential
/// (<see cref="InvitationRedemption"/>).</item>
/// </list>
/// Each link works once and for 7 days; resending issues a new link and voids the previous one.
/// </summary>
internal static class InvitationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var invitations = endpoints.MapGroup("/invitations");

        invitations.MapGet("", List)
            .WithName("ListPlatformInvitations")
            .RequirePermission(PlatformPermissions.StaffRead);

        invitations.MapPost("", Create)
            .WithName("CreatePlatformInvitation")
            .RequirePermission(PlatformPermissions.StaffManage);

        invitations.MapPost("/{invitationId:guid}/resend", Resend)
            .WithName("ResendPlatformInvitation")
            .RequirePermission(PlatformPermissions.StaffManage);

        invitations.MapPost("/{invitationId:guid}/revoke", Revoke)
            .WithName("RevokePlatformInvitation")
            .RequirePermission(PlatformPermissions.StaffManage);

        invitations.MapPost("/lookup", Lookup)
            .WithName("LookupPlatformInvitation")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Anonymous);

        invitations.MapPost("/accept", Accept)
            .WithName("AcceptPlatformInvitation")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Anonymous);
    }

    /// <summary>Open invitations (not accepted, not revoked), newest first; expired ones included, to resend.</summary>
    internal static async Task<Ok<IReadOnlyList<InvitationResponse>>> List(ShelterDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var invitations = await db.Set<StaffInvitation>()
            .AsNoTracking()
            .Where(i => i.AcceptedAt == null && i.RevokedAt == null)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<InvitationResponse>>([.. invitations.Select(i => ToResponse(i, now))]);
    }

    internal static async Task<Results<Created<InvitationResponse>, ValidationProblem, ProblemHttpResult>> Create(
        CreateInvitationRequest request,
        ShelterDbContext db,
        ITenantContext tenantContext,
        IUserContext userContext,
        UserManager<UserAccount> userManager,
        IEmailSender emailSender,
        PublicLinks links,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var email = request.Email?.Trim();
        if (email is null || email.Length > 256 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            errors["email"] = ["A valid email address is required."];
        }

        if (!StaffMembership.AreValid(request.Roles))
        {
            errors["roles"] = [$"One or more of: {string.Join(", ", SystemRoles.All.Order(StringComparer.Ordinal))}."];
        }

        if (request.Language is not null and not (Languages.French or Languages.English))
        {
            errors["language"] = ["fr or en."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var now = timeProvider.GetUtcNow();
        var normalizedEmail = userManager.NormalizeEmail(email)!;
        var account = await userManager.FindByEmailAsync(email!);
        if (account is not null && await db.Set<StaffMembership>().AnyAsync(m => m.UserId == account.Id, cancellationToken))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This person already has a membership in the organization.");
        }

        var open = await db.Set<StaffInvitation>()
            .SingleOrDefaultAsync(i => i.NormalizedEmail == normalizedEmail && i.AcceptedAt == null && i.RevokedAt == null, cancellationToken);
        if (open is not null)
        {
            if (open.IsPending(now))
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "An invitation is already pending for this email; resend it instead.");
            }

            // Expired: closed so the new one can take its place.
            open.Revoke(now);
            await DeleteTokensAsync(db, open.Id, cancellationToken);
        }

        var invitation = new StaffInvitation(email!, normalizedEmail, request.Roles!, request.Language ?? Languages.French, userContext.UserId!.Value, now);
        db.Set<StaffInvitation>().Add(invitation);
        await db.SaveChangesAsync(cancellationToken);

        await IssueAsync(db, invitation, tenantContext.RequireTenantId(), account?.PreferredLanguage, emailSender, links, cancellationToken);
        return TypedResults.Created($"/api/platform/invitations/{invitation.Id:D}", ToResponse(invitation, now));
    }

    /// <summary>A new link with a new 7-day validity; the previous link stops working.</summary>
    internal static async Task<Results<Ok<InvitationResponse>, NotFound, ProblemHttpResult>> Resend(
        Guid invitationId,
        ShelterDbContext db,
        ITenantContext tenantContext,
        IUserContext userContext,
        UserManager<UserAccount> userManager,
        IEmailSender emailSender,
        PublicLinks links,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var invitation = await db.Set<StaffInvitation>().SingleOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
        if (invitation is null)
        {
            return TypedResults.NotFound();
        }

        if (!invitation.IsOpen)
        {
            return Closed();
        }

        var now = timeProvider.GetUtcNow();
        invitation.Renew(userContext.UserId!.Value, now);
        await DeleteTokensAsync(db, invitation.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var account = await userManager.FindByEmailAsync(invitation.Email);
        await IssueAsync(db, invitation, tenantContext.RequireTenantId(), account?.PreferredLanguage, emailSender, links, cancellationToken);
        return TypedResults.Ok(ToResponse(invitation, now));
    }

    internal static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Revoke(
        Guid invitationId,
        ShelterDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var invitation = await db.Set<StaffInvitation>().SingleOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
        if (invitation is null)
        {
            return TypedResults.NotFound();
        }

        if (!invitation.IsOpen)
        {
            return Closed();
        }

        invitation.Revoke(timeProvider.GetUtcNow());
        await DeleteTokensAsync(db, invitation.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>What the accept page needs: the organization, the invited email, and whether to sign in or sign up.</summary>
    internal static async Task<Results<Ok<InvitationLookupResponse>, ProblemHttpResult>> Lookup(
        InvitationTokenRequest request,
        InvitationRedemption redemption,
        CancellationToken cancellationToken)
    {
        var preview = await redemption.PreviewAsync(request.Token, cancellationToken);
        return preview is null
            ? InvalidLink()
            : TypedResults.Ok(new InvitationLookupResponse(preview.OrganizationName, preview.Email, preview.AccountExists));
    }

    /// <summary>
    /// Accepts the invitation. Without an account for the email: <c>displayName</c> and <c>password</c> create one.
    /// With one: the caller must be signed in as that account (403 otherwise). Does not sign in.
    /// </summary>
    internal static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> Accept(
        AcceptInvitationRequest request,
        ClaimsPrincipal principal,
        InvitationRedemption redemption,
        CancellationToken cancellationToken)
    {
        Guid? userId = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        var result = await redemption.AcceptAsync(request.Token, request.DisplayName, request.Password, userId, cancellationToken);
        return result.Outcome switch
        {
            AcceptOutcome.Accepted => TypedResults.NoContent(),
            AcceptOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
            AcceptOutcome.SignInRequired => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Sign in with the invited account to accept."),
            AcceptOutcome.AlreadyMember => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This account already has a membership in the organization."),
            _ => InvalidLink(),
        };
    }

    /// <summary>Stores a new secret's hash and emails the link, in the account's language when it has one.</summary>
    private static async Task IssueAsync(
        ShelterDbContext db,
        StaffInvitation invitation,
        Guid organizationId,
        string? accountLanguage,
        IEmailSender emailSender,
        PublicLinks links,
        CancellationToken cancellationToken)
    {
        var (token, hash) = InvitationSecret.Create();
        db.Set<InvitationToken>().Add(new InvitationToken(hash, organizationId, invitation.Id, invitation.ExpiresAt));
        await db.SaveChangesAsync(cancellationToken);

        var organizationName = await db.Set<Organization>().Where(o => o.Id == organizationId).Select(o => o.Name).SingleAsync(cancellationToken);

        // Sent inside the transaction: if the server refuses it, the invitation is rolled back and the call fails.
        await emailSender.SendAsync(
            PlatformEmails.Invitation(invitation.Email, accountLanguage ?? invitation.Language, organizationName, links.AcceptInvitation(token), invitation.ExpiresAt),
            cancellationToken);
    }

    private static Task<int> DeleteTokensAsync(ShelterDbContext db, Guid invitationId, CancellationToken cancellationToken) =>
        db.Set<InvitationToken>().Where(t => t.InvitationId == invitationId).ExecuteDeleteAsync(cancellationToken);

    private static InvitationResponse ToResponse(StaffInvitation invitation, DateTimeOffset now) => new(
        invitation.Id,
        invitation.Email,
        invitation.RoleKeys,
        invitation.Language,
        invitation.IsPending(now) ? InvitationStatus.Pending : InvitationStatus.Expired,
        invitation.CreatedAt,
        invitation.ExpiresAt);

    private static ProblemHttpResult Closed() =>
        TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "The invitation is already accepted or revoked.");

    private static ProblemHttpResult InvalidLink() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "This invitation link is invalid or has expired.");
}

/// <summary>A new invitation.</summary>
/// <param name="Email">Where to send it.</param>
/// <param name="Roles">System role keys the membership will have: <c>administrator</c>, <c>staff</c>, <c>read_only</c>.</param>
/// <param name="Language"><c>fr</c> (default) or <c>en</c>: the email's language when the person has no account yet.</param>
internal sealed record CreateInvitationRequest(string? Email, IReadOnlyList<string>? Roles, string? Language);

/// <summary>Values of <see cref="InvitationResponse.Status"/>.</summary>
internal static class InvitationStatus
{
    /// <summary>The link can be accepted.</summary>
    public const string Pending = "pending";

    /// <summary>Past its 7 days; can be resent.</summary>
    public const string Expired = "expired";
}

/// <summary>An open invitation.</summary>
/// <param name="Id">Invitation ID.</param>
/// <param name="Email">Where it was sent.</param>
/// <param name="Roles">System role keys the membership will have.</param>
/// <param name="Language"><c>fr</c> or <c>en</c>.</param>
/// <param name="Status"><c>pending</c> or <c>expired</c>.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="ExpiresAt">When the current link stops working.</param>
internal sealed record InvitationResponse(Guid Id, string Email, IReadOnlyList<string> Roles, string Language, string Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

/// <summary>The secret from an invitation link.</summary>
/// <param name="Token">The <c>token</c> value from the link's fragment.</param>
internal sealed record InvitationTokenRequest(string? Token);

/// <summary>An invitation, as its recipient sees it.</summary>
/// <param name="OrganizationName">The inviting organization.</param>
/// <param name="Email">The invited email.</param>
/// <param name="AccountExists">Whether the email already has an account: sign in, then accept; otherwise choose a name and password.</param>
internal sealed record InvitationLookupResponse(string OrganizationName, string Email, bool AccountExists);

/// <summary>Accepting an invitation.</summary>
/// <param name="Token">The <c>token</c> value from the link's fragment.</param>
/// <param name="DisplayName">Name for the new account; ignored when the email has an account.</param>
/// <param name="Password">Password for the new account (12 characters or more); ignored when the email has an account.</param>
internal sealed record AcceptInvitationRequest(string? Token, string? DisplayName, string? Password);
