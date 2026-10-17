using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.DevSeed;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Features.DevSeed;

/// <summary>
/// Development seed (ADR 0023): a staff account and a read-only account (password <c>DevSeed:AdminPassword</c>) with a
/// membership in the seeded organization, and two pending invitations without a link (resend issues one). Accounts are
/// reused when they exist, so every seeded organization shares the same sign-ins.
/// </summary>
internal sealed class PlatformDevSeed(
    ShelterDbContext db,
    UserManager<UserAccount> userManager,
    IUserContext user,
    IConfiguration configuration) : IDevSeedContributor
{
    private static readonly (string Email, string DisplayName, string Role)[] Accounts =
    [
        ("staff@refuge.local", "Employé démo", SystemRoles.Staff),
        ("lecture@refuge.local", "Lecture démo", SystemRoles.ReadOnly),
    ];

    private static readonly (string Email, string Role)[] Invitations =
    [
        ("nouvelle.benevole@example.com", SystemRoles.ReadOnly),
        ("coordination@example.com", SystemRoles.Staff),
    ];

    public int Order => 5;

    public async Task SeedAsync(DevSeedContext context, CancellationToken cancellationToken)
    {
        var password = configuration["DevSeed:AdminPassword"]
            ?? throw new InvalidOperationException("DevSeed:AdminPassword is not configured.");

        foreach (var (email, displayName, role) in Accounts)
        {
            var account = await userManager.FindByEmailAsync(email);
            if (account is null)
            {
                account = new UserAccount(email, displayName, Languages.French, context.Now) { EmailConfirmed = true };
                var result = await userManager.CreateAsync(account, password);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Dev seed account could not be created: " + string.Join(", ", result.Errors.Select(e => e.Code)));
                }

                context.Count("platform.accounts");
            }

            var accountId = account.Id;
            if (!await db.Set<StaffMembership>().AnyAsync(m => m.UserId == accountId, cancellationToken))
            {
                db.Set<StaffMembership>().Add(new StaffMembership(accountId, [role], context.Now));
                context.Count("platform.memberships");
            }
        }

        var invitedBy = user.UserId ?? throw new InvalidOperationException("The dev seed runs as a signed-in account.");
        foreach (var (email, role) in Invitations)
        {
            var normalized = userManager.NormalizeEmail(email)!;
            if (await db.Set<StaffInvitation>().AnyAsync(
                    i => i.NormalizedEmail == normalized && i.AcceptedAt == null && i.RevokedAt == null, cancellationToken))
            {
                continue;
            }

            db.Set<StaffInvitation>().Add(new StaffInvitation(email, normalized, [role], Languages.French, invitedBy, context.Now));
            context.Count("platform.invitations");
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
