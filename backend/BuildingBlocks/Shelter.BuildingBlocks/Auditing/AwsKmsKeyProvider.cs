namespace Shelter.BuildingBlocks.Auditing;

/// <summary>
/// Production key provider: AWS KMS in ca-central-1 (ADR 0012, 0016). A stub until deployment lands; selecting it
/// (<c>Audit:KeyProvider = AwsKms</c>) fails on first use rather than falling back to anything weaker.
/// </summary>
internal sealed class AwsKmsKeyProvider : IKeyProvider
{
    public ValueTask<byte[]> WrapKeyAsync(ReadOnlyMemory<byte> dataKey, DataKeyContext context, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("The AWS KMS key provider is added with deployment (ADR 0016).");

    public ValueTask<byte[]> UnwrapKeyAsync(ReadOnlyMemory<byte> wrappedKey, DataKeyContext context, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("The AWS KMS key provider is added with deployment (ADR 0016).");
}
