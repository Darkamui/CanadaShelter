using Shelter.BuildingBlocks.Auditing;

namespace Shelter.Testing;

/// <summary>A fixed audit master key for tests only; never used outside test hosts and contexts.</summary>
public static class TestKeys
{
    /// <summary>Base64 of 32 bytes 0x00..0x1F.</summary>
    public static readonly string MasterKeyBase64 = Convert.ToBase64String([.. Enumerable.Range(0, 32).Select(i => (byte)i)]);

    /// <summary>A <see cref="LocalMasterKeyProvider"/> on <see cref="MasterKeyBase64"/>.</summary>
    public static IKeyProvider Provider { get; } = LocalMasterKeyProvider.FromBase64(MasterKeyBase64);
}
