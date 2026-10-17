using System.Globalization;
using System.Security.Cryptography;

namespace SuperApp.Tests.Api;

/// <summary>Makes the 6-digit code an authenticator app would show for a key (RFC 6238, 30 seconds, SHA-1).</summary>
public static class Totp
{
    public static string Code(string sharedKey, DateTime? at = null)
    {
        var key = Base32(sharedKey.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant());
        var step = (long)((at ?? DateTime.UtcNow) - DateTime.UnixEpoch).TotalSeconds / 30;
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }
#pragma warning disable CA5350 // TOTP is defined on HMAC-SHA1
        var hash = HMACSHA1.HashData(key, counter);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0F;
        var value = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] Base32(string text)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = string.Concat(text.TrimEnd('=').Select(c => Convert.ToString(alphabet.IndexOf(c, StringComparison.Ordinal), 2).PadLeft(5, '0')));
        return Enumerable.Range(0, bits.Length / 8).Select(i => Convert.ToByte(bits.Substring(i * 8, 8), 2)).ToArray();
    }
}
