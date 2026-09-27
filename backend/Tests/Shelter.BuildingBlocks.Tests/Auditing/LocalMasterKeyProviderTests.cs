using System.Security.Cryptography;
using Shelter.BuildingBlocks.Auditing;

namespace Shelter.BuildingBlocks.Tests.Auditing;

/// <summary>M1-6: the local master key wraps data keys with authenticated encryption bound to their owner.</summary>
public sealed class LocalMasterKeyProviderTests
{
    private static readonly LocalMasterKeyProvider Provider = new(RandomNumberGenerator.GetBytes(32));
    private static readonly DataKeyContext Owner = new(Guid.CreateVersion7(), Guid.CreateVersion7());

    [Fact]
    public async Task Wrapped_key_unwraps_to_the_same_key()
    {
        var dataKey = RandomNumberGenerator.GetBytes(32);

        var wrapped = await Provider.WrapKeyAsync(dataKey, Owner, TestContext.Current.CancellationToken);

        Assert.False(wrapped.AsSpan().IndexOf(dataKey) >= 0);
        Assert.Equal(dataKey, await Provider.UnwrapKeyAsync(wrapped, Owner, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Wrapped_key_cannot_be_unwrapped_for_another_subject_or_tenant()
    {
        var wrapped = await Provider.WrapKeyAsync(RandomNumberGenerator.GetBytes(32), Owner, TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await Provider.UnwrapKeyAsync(wrapped, Owner with { SubjectId = Guid.CreateVersion7() }, TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await Provider.UnwrapKeyAsync(wrapped, Owner with { TenantId = Guid.CreateVersion7() }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Wrapped_key_cannot_be_unwrapped_with_another_master_key()
    {
        var wrapped = await Provider.WrapKeyAsync(RandomNumberGenerator.GetBytes(32), Owner, TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await new LocalMasterKeyProvider(RandomNumberGenerator.GetBytes(32)).UnwrapKeyAsync(wrapped, Owner, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Master_key_must_be_256_bits()
    {
        Assert.Throws<ArgumentException>(() => new LocalMasterKeyProvider(new byte[16]));
        Assert.Throws<InvalidOperationException>(() => LocalMasterKeyProvider.FromBase64("not base64!"));
    }
}
