using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace Shelter.IntegrationTests.Identity;

/// <summary>What an authenticator app computes (RFC 6238: HMAC-SHA1, 30-second steps, 6 digits) from a base32 key.</summary>
internal static class Totp
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>The code for the current time step, shifted by <paramref name="stepOffset"/> steps.</summary>
    public static string Code(string base32Key, int stepOffset = 0)
    {
        var step = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30) + stepOffset;
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);

#pragma warning disable CA5350 // RFC 6238 authenticator apps use HMAC-SHA1; this mirrors them.
        var hash = HMACSHA1.HashData(Decode(base32Key), counter);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0F;
        var binary = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & 0x7FFFFFFF;
        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] Decode(string base32)
    {
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in base32.TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | Base32Alphabet.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
                buffer &= (1 << bits) - 1;
            }
        }

        return [.. bytes];
    }
}
