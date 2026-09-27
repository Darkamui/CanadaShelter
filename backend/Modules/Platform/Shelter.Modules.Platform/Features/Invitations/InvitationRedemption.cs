using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Features.Invitations;

/// <summary>
/// Reads and accepts an invitation from its secret alone (M2-5, ADR 0018). The global <see cref="InvitationToken"/>
/// gives the organization; the work then runs in a <b>new DI scope</b> whose tenant is that organization, in one unit
/// of work (like a tenant job), because the request's own scope may already belong to another organization (a
/// signed-in user accepting an invitation elsewhere). An unknown, expired, revoked or used secret all read as
/// "not found".
/// </summary>
internal sealed class InvitationRedemption(IServiceScopeFactory scopeFactory, AuditContext requestAudit, TimeProvider timeProvider)
{
    /// <summary>What the accept page shows, or <see langword="null"/>.</summary>
    public Task<InvitationPreview?> PreviewAsync(string? token, CancellationToken cancellationToken) =>
        RunAsync<InvitationPreview?>(
            token,
            notFound: null,
            async (services, invitation, account, ct) =>
            {
                var organizationName = await services.GetRequiredService<ShelterDbContext>().Set<Organization>()
                    .Where(o => o.Id == invitation.TenantId)
                    .Select(o => o.Name)
                    .SingleAsync(ct);
                return new InvitationPreview(organizationName, invitation.Email, account is not null);
            },
            static _ => false,
            cancellationToken);

    /// <summary>
    /// Accepts: creates the account when the email has none (name and password required), or, when it has one,
    /// requires <paramref name="signedInUserId"/> to be that account. Then the membership with the invitation's roles;
    /// the invitation is marked accepted and its secret deleted. All or nothing.
    /// </summary>
    public Task<AcceptResult> AcceptAsync(string? token, string? displayName, string? password, Guid? signedInUserId, CancellationToken cancellationToken) =>
        RunAsync(
            token,
            notFound: AcceptResult.NotFound,
            async (services, invitation, account, ct) =>
            {
                var db = services.GetRequiredService<ShelterDbContext>();
                var now = timeProvider.GetUtcNow();

                if (account is null)
                {
                    var errors = new Dictionary<string, string[]>();
                    if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 200)
                    {
                        errors["displayName"] = ["A name of 1 to 200 characters is required."];
                    }

                    if (string.IsNullOrEmpty(password))
                    {
                        errors["password"] = ["A password is required."];
                    }

                    if (errors.Count > 0)
                    {
                        return AcceptResult.Invalid(errors);
                    }

                    // The secret proves the person reads this mailbox: the address is confirmed.
                    account = new UserAccount(invitation.Email, displayName!.Trim(), invitation.Language, now) { EmailConfirmed = true };
                    var created = await services.GetRequiredService<UserManager<UserAccount>>().CreateAsync(account, password!);
                    if (!created.Succeeded)
                    {
                        return AcceptResult.Invalid(new Dictionary<string, string[]>
                        {
                            ["password"] = [.. created.Errors.Select(e => e.Description)],
                        });
                    }
                }
                else if (account.Id != signedInUserId)
                {
                    // The email matches an account, but only that account's owner may take the membership.
                    return AcceptResult.SignInRequired;
                }

                if (await db.Set<StaffMembership>().AnyAsync(m => m.UserId == account.Id, ct))
                {
                    return AcceptResult.AlreadyMember;
                }

                db.Set<StaffMembership>().Add(new StaffMembership(account.Id, invitation.RoleKeys, now));
                invitation.Accept(now);
                await db.SaveChangesAsync(ct);
                await db.Set<InvitationToken>().Where(t => t.InvitationId == invitation.Id).ExecuteDeleteAsync(ct);
                return AcceptResult.Accepted;
            },
            static result => result.Outcome == AcceptOutcome.Accepted,
            cancellationToken);

    private async Task<T> RunAsync<T>(
        string? token,
        T notFound,
        Func<IServiceProvider, StaffInvitation, UserAccount?, CancellationToken, Task<T>> work,
        Func<T, bool> shouldCommit,
        CancellationToken cancellationToken)
    {
        var hash = InvitationSecret.Hash(token);
        if (hash is null)
        {
            return notFound;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ShelterDbContext>();
        var now = timeProvider.GetUtcNow();

        // Global table, read before any tenant is set.
        var entry = await db.Set<InvitationToken>().AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (entry is null || entry.ExpiresAt <= now)
        {
            return notFound;
        }

        services.GetRequiredService<TenantContext>().Set(entry.OrganizationId);
        services.GetRequiredService<AuditContext>()
            .Set(requestAudit.ActorType, requestAudit.ActorId, requestAudit.Source, requestAudit.CorrelationId);

        return await services.GetRequiredService<UnitOfWork>().ExecuteAsync(
            async ct =>
            {
                // Lock the invitation: two concurrent accepts of one link serialize here; the second sees it accepted.
                var locked = await db.Database
                    .SqlQuery<Guid>($"""SELECT id AS "Value" FROM platform.staff_invitation WHERE id = {entry.InvitationId} FOR UPDATE""")
                    .ToListAsync(ct);
                var invitation = locked.Count == 0
                    ? null
                    : await db.Set<StaffInvitation>().SingleOrDefaultAsync(i => i.Id == entry.InvitationId, ct);
                if (invitation is null || !invitation.IsPending(now))
                {
                    return notFound;
                }

                var account = await services.GetRequiredService<UserManager<UserAccount>>().FindByEmailAsync(invitation.Email);
                return await work(services, invitation, account, ct);
            },
            shouldCommit,
            cancellationToken);
    }
}

/// <summary>An invitation as its recipient sees it.</summary>
internal sealed record InvitationPreview(string OrganizationName, string Email, bool AccountExists);

/// <summary>How an accept ended.</summary>
internal enum AcceptOutcome
{
    Accepted,
    NotFound,
    Invalid,
    SignInRequired,
    AlreadyMember,
}

/// <summary>An accept's outcome, with validation errors for <see cref="AcceptOutcome.Invalid"/>.</summary>
internal sealed record AcceptResult(AcceptOutcome Outcome, IDictionary<string, string[]>? Errors = null)
{
    public static readonly AcceptResult Accepted = new(AcceptOutcome.Accepted);
    public static readonly AcceptResult NotFound = new(AcceptOutcome.NotFound);
    public static readonly AcceptResult SignInRequired = new(AcceptOutcome.SignInRequired);
    public static readonly AcceptResult AlreadyMember = new(AcceptOutcome.AlreadyMember);

    public static AcceptResult Invalid(IDictionary<string, string[]> errors) => new(AcceptOutcome.Invalid, errors);
}
