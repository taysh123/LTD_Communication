namespace LTD_Communication.Vulnerable.Helpers;

// VULNERABLE: Passwords are stored and compared as plain text.
// No hashing, no salting — a database breach exposes all passwords immediately.
public static class PasswordHelper
{
    /// <summary>
    /// VULNERABLE: "Hashing" returns the password unchanged.
    /// Plain-text passwords are written directly to the database.
    /// </summary>
    public static string HashPassword(string password) => password;

    /// <summary>
    /// VULNERABLE: Direct string equality comparison — no constant-time check.
    /// </summary>
    public static bool VerifyPassword(string inputPassword, string storedPassword, string? salt = null)
        => inputPassword == storedPassword;
}
