using System.Security.Cryptography;
using System.Text;

namespace LTD_Communication.Secure.Services;

public static class TokenService
{
    /// <summary>Generates a cryptographically random, URL-safe token.</summary>
    public static string GenerateToken() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Hashes the token with SHA-1 for database storage (per project specification).
    /// The raw token travels in the URL; only the hash is stored — so even a DB
    /// breach does not yield usable reset tokens.
    /// </summary>
    public static string HashToken(string token)
    {
        using var sha1 = SHA1.Create();
        byte[] bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
