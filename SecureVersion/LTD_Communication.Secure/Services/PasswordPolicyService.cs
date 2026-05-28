using LTD_Communication.Secure.Helpers;
using Microsoft.Data.SqlClient;

namespace LTD_Communication.Secure.Services;

// Reads all policy rules from appsettings.json → PasswordPolicy section.
// No policy logic is hard-coded — change the config, the rules change instantly.
public class PasswordPolicyService
{
    private readonly IConfiguration _config;
    private readonly DbHelper _db;

    public PasswordPolicyService(IConfiguration config, DbHelper db)
    {
        _config = config;
        _db = db;
    }

    /// <summary>
    /// Returns a list of human-readable error messages.
    /// An empty list means the password passes all rules.
    /// Pass userId to also check password history.
    /// </summary>
    public List<string> ValidatePassword(string password, int? userId = null)
    {
        var errors = new List<string>();
        var policy = _config.GetSection("PasswordPolicy");

        int minLength = policy.GetValue<int>("MinLength");
        if (password.Length < minLength)
            errors.Add($"Password must be at least {minLength} characters long.");

        if (policy.GetValue<bool>("RequireUppercase") && !password.Any(char.IsUpper))
            errors.Add("Password must contain at least one uppercase letter (A-Z).");

        if (policy.GetValue<bool>("RequireLowercase") && !password.Any(char.IsLower))
            errors.Add("Password must contain at least one lowercase letter (a-z).");

        if (policy.GetValue<bool>("RequireDigit") && !password.Any(char.IsDigit))
            errors.Add("Password must contain at least one digit (0-9).");

        if (policy.GetValue<bool>("RequireSpecialChar") && !password.Any(c => !char.IsLetterOrDigit(c)))
            errors.Add("Password must contain at least one special character (e.g. @, !, #, $).");

        var dictionaryList = policy.GetSection("DictionaryPasswords").Get<List<string>>() ?? new();
        if (dictionaryList.Contains(password.ToLowerInvariant()))
            errors.Add("Password is too common. Please choose a more unique password.");

        // History check — only run if basic rules already pass (avoids hashing a bad password)
        if (userId.HasValue && errors.Count == 0)
        {
            int historyCount = policy.GetValue<int>("PasswordHistoryCount");
            var history = _db.ExecuteQuery(
                "SELECT TOP (@Count) PasswordHash, Salt FROM PasswordHistory WHERE UserId = @UserId ORDER BY ChangedAt DESC",
                new SqlParameter("@Count",  historyCount),
                new SqlParameter("@UserId", userId.Value));

            foreach (var row in history)
            {
                string storedHash = row["PasswordHash"]?.ToString() ?? "";
                string salt       = row["Salt"]?.ToString()         ?? "";
                if (!string.IsNullOrEmpty(salt) && PasswordHelper.VerifyPassword(password, storedHash, salt))
                {
                    errors.Add($"Password cannot match any of your last {historyCount} passwords.");
                    break;
                }
            }
        }

        return errors;
    }
}
