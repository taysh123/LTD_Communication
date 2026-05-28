using System.Security.Cryptography;

namespace LTD_Communication.Secure.Helpers;

// SECURE: PBKDF2-SHA256 with 100,000 iterations and a cryptographic random salt.
// Each password gets a unique salt, making pre-computed (rainbow table) attacks infeasible.
public static class PasswordHelper
{
    private const int SaltSize  = 32;       // 256-bit salt
    private const int HashSize  = 32;       // 256-bit derived key
    private const int Iterations = 100_000; // NIST SP 800-132 recommended minimum

    /// <summary>
    /// Returns a (hash, salt) tuple. Both are Base64-encoded strings safe for database storage.
    /// </summary>
    public static (string hash, string salt) HashPassword(string password)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        string salt = Convert.ToBase64String(saltBytes);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
        string hash = Convert.ToBase64String(pbkdf2.GetBytes(HashSize));

        return (hash, salt);
    }

    /// <summary>
    /// Compares using a constant-time equality check to prevent timing-oracle attacks.
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
        byte[] computedBytes = pbkdf2.GetBytes(HashSize);
        byte[] storedBytes   = Convert.FromBase64String(storedHash);
        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }
}
