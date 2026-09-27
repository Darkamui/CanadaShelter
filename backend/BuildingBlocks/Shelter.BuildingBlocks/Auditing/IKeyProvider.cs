namespace Shelter.BuildingBlocks.Auditing;

/// <summary>
/// Wraps and unwraps per-person data keys with a master key the application never stores (ADR 0011, 0016).
/// Locally: <see cref="LocalMasterKeyProvider"/>. In production: a KMS provider.
/// </summary>
public interface IKeyProvider
{
    /// <summary>Encrypts <paramref name="dataKey"/>; the result is bound to <paramref name="context"/>.</summary>
    ValueTask<byte[]> WrapKeyAsync(ReadOnlyMemory<byte> dataKey, DataKeyContext context, CancellationToken cancellationToken = default);

    /// <summary>Decrypts a key wrapped for the same <paramref name="context"/>.</summary>
    ValueTask<byte[]> UnwrapKeyAsync(ReadOnlyMemory<byte> wrappedKey, DataKeyContext context, CancellationToken cancellationToken = default);
}

/// <summary>Whose data key it is; authenticated with the wrapped key so it cannot be moved to another row.</summary>
public readonly record struct DataKeyContext(Guid TenantId, Guid SubjectId);
