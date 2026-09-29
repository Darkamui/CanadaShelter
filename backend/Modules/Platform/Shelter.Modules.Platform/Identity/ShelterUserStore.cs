using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Identity;

/// <summary>
/// Identity's EF store, except that MFA recovery codes are kept as SHA-256 hashes (M2-4): Identity stores them in
/// plain text in <c>user_token</c>, and each one is a way past the second factor. Codes are shown once at enrollment
/// and only ever compared, so a hash is enough. The authenticator key must stay readable to check codes.
/// </summary>
internal sealed class ShelterUserStore(ShelterDbContext context, IdentityErrorDescriber? describer = null)
    : UserOnlyStore<UserAccount, ShelterDbContext, Guid>(context, describer)
{
    // Identity's own token names, so codes issued before this store stay countable.
    private const string CodesProvider = "[AspNetUserStore]";
    private const string CodesName = "RecoveryCodes";

    public override Task ReplaceCodesAsync(UserAccount user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recoveryCodes);
        return SetTokenAsync(user, CodesProvider, CodesName, string.Join(';', recoveryCodes.Select(Hash)), cancellationToken);
    }

    // Not through the base: it writes the remaining codes back through ReplaceCodesAsync, which would hash them again.
    public override async Task<bool> RedeemCodeAsync(UserAccount user, string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        var codes = await StoredCodesAsync(user, cancellationToken);
        if (!codes.Remove(Hash(code)))
        {
            return false;
        }

        await SetTokenAsync(user, CodesProvider, CodesName, string.Join(';', codes), cancellationToken);
        return true;
    }

    public override async Task<int> CountCodesAsync(UserAccount user, CancellationToken cancellationToken = default) =>
        (await StoredCodesAsync(user, cancellationToken)).Count;

    private async Task<List<string>> StoredCodesAsync(UserAccount user, CancellationToken cancellationToken) =>
        [.. ((await GetTokenAsync(user, CodesProvider, CodesName, cancellationToken)) ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries)];

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
