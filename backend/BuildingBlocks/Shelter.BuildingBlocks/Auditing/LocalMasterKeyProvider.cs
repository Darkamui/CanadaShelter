using System.Security.Cryptography;
using System.Text;

namespace Shelter.BuildingBlocks.Auditing;

/// <summary>
/// Wraps data keys with AES-256-GCM under a 256-bit master key from configuration (<c>Audit:MasterKey</c>,
/// base64). For local development and tests; production keeps the master key in KMS (ADR 0016).
/// Wrapped format: version byte (1) | nonce (12) | ciphertext | tag (16).
/// </summary>
public sealed class LocalMasterKeyProvider : IKeyProvider
{
    private const byte Version = 1;

    private readonly byte[] _masterKey;

    /// <summary>Creates the provider from a 32-byte master key.</summary>
    public LocalMasterKeyProvider(byte[] masterKey)
    {
        ArgumentNullException.ThrowIfNull(masterKey);
        if (masterKey.Length != AesGcmEnvelope.KeySize)
        {
            throw new ArgumentException($"The master key must be {AesGcmEnvelope.KeySize} bytes.", nameof(masterKey));
        }

        _masterKey = [.. masterKey];
    }

    /// <summary>Creates the provider from a base64 master key, as configured.</summary>
    public static LocalMasterKeyProvider FromBase64(string masterKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(masterKey);

        byte[] key;
        try
        {
            key = Convert.FromBase64String(masterKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Audit:MasterKey is not valid base64.", ex);
        }

        return new LocalMasterKeyProvider(key);
    }

    /// <inheritdoc />
    public ValueTask<byte[]> WrapKeyAsync(ReadOnlyMemory<byte> dataKey, DataKeyContext context, CancellationToken cancellationToken = default)
    {
        var sealedKey = AesGcmEnvelope.Seal(_masterKey, dataKey.Span, AssociatedData(context));
        return ValueTask.FromResult<byte[]>([Version, .. sealedKey]);
    }

    /// <inheritdoc />
    public ValueTask<byte[]> UnwrapKeyAsync(ReadOnlyMemory<byte> wrappedKey, DataKeyContext context, CancellationToken cancellationToken = default)
    {
        var span = wrappedKey.Span;
        if (span.IsEmpty || span[0] != Version)
        {
            throw new CryptographicException("Unknown wrapped key version.");
        }

        return ValueTask.FromResult(AesGcmEnvelope.Open(_masterKey, span[1..], AssociatedData(context)));
    }

    private static byte[] AssociatedData(DataKeyContext context) =>
        Encoding.UTF8.GetBytes($"shelter-dek-v1|{context.TenantId:D}|{context.SubjectId:D}");
}

/// <summary>AES-256-GCM with a random nonce: nonce (12) | ciphertext | tag (16).</summary>
internal static class AesGcmEnvelope
{
    public const int KeySize = 32;

    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static byte[] Seal(byte[] key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        var output = new byte[NonceSize + plaintext.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, output.AsSpan(NonceSize, plaintext.Length), output.AsSpan(NonceSize + plaintext.Length), associatedData);
        return output;
    }

    public static byte[] Open(byte[] key, ReadOnlySpan<byte> envelope, ReadOnlySpan<byte> associatedData)
    {
        if (envelope.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("The encrypted value is truncated.");
        }

        var ciphertextLength = envelope.Length - NonceSize - TagSize;
        var plaintext = new byte[ciphertextLength];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(envelope[..NonceSize], envelope.Slice(NonceSize, ciphertextLength), envelope[(NonceSize + ciphertextLength)..], plaintext, associatedData);
        return plaintext;
    }
}
