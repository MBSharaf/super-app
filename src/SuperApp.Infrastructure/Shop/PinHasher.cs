using System.Security.Cryptography;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Factory staff PINs (4 digits) are stored as salt + PBKDF2-SHA256 (48 bytes), never as text.</summary>
public static class PinHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public static byte[] Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return [.. salt, .. Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize)];
    }

    public static bool Verify(string pin, byte[] stored)
    {
        if (stored.Length != SaltSize + HashSize)
        {
            return false;
        }
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, stored.AsSpan(0, SaltSize).ToArray(), Iterations, HashAlgorithmName.SHA256, HashSize);
        return CryptographicOperations.FixedTimeEquals(hash, stored.AsSpan(SaltSize));
    }
}
