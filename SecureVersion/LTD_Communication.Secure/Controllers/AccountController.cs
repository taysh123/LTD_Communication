using LTD_Communication.Secure.Filters;
using LTD_Communication.Secure.Helpers;
using LTD_Communication.Secure.Models.ViewModels;
using LTD_Communication.Secure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace LTD_Communication.Secure.Controllers;

public class AccountController : Controller
{
    private readonly DbHelper _db;
    private readonly PasswordPolicyService _policy;
    private readonly IConfiguration _config;

    public AccountController(DbHelper db, PasswordPolicyService policy, IConfiguration config)
    {
        _db     = db;
        _policy = policy;
        _config = config;
    }

    // ---- REGISTER ------------------------------------------------

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public IActionResult Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // SECURE: Validate against password policy before accepting
        var policyErrors = _policy.ValidatePassword(model.Password);
        foreach (var error in policyErrors)
            ModelState.AddModelError("Password", error);

        if (!ModelState.IsValid) return View(model);

        // SECURE: Parameterized query — SQL Injection not possible
        var exists = _db.ExecuteScalar(
            "SELECT COUNT(*) FROM Users WHERE Username = @Username OR Email = @Email",
            new SqlParameter("@Username", model.Username),
            new SqlParameter("@Email",    model.Email));

        if (Convert.ToInt32(exists) > 0)
        {
            ModelState.AddModelError("", "Username or email already exists.");
            return View(model);
        }

        // SECURE: PBKDF2 hash with unique salt — never plain text
        var (hash, salt) = PasswordHelper.HashPassword(model.Password);

        _db.ExecuteNonQuery(
            "INSERT INTO Users (Username, Email, PasswordHash, Salt) VALUES (@Username, @Email, @Hash, @Salt)",
            new SqlParameter("@Username", model.Username),
            new SqlParameter("@Email",    model.Email),
            new SqlParameter("@Hash",     hash),
            new SqlParameter("@Salt",     salt));

        // Save initial password to history to prevent immediate reuse
        _db.ExecuteNonQuery(
            @"INSERT INTO PasswordHistory (UserId, PasswordHash, Salt)
              SELECT Id, @Hash, @Salt FROM Users WHERE Username = @Username",
            new SqlParameter("@Hash",     hash),
            new SqlParameter("@Salt",     salt),
            new SqlParameter("@Username", model.Username));

        TempData["Success"] = "Registration successful. Please login.";
        return RedirectToAction("Login");
    }

    // ---- LOGIN ---------------------------------------------------

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public IActionResult Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        int maxAttempts = _config.GetSection("PasswordPolicy").GetValue<int>("MaxLoginAttempts");

        // SECURE: Fetch user by username only — password verified via hash, not SQL WHERE
        var rows = _db.ExecuteQuery(
            "SELECT * FROM Users WHERE Username = @Username",
            new SqlParameter("@Username", model.Username));

        if (rows.Count == 0)
        {
            // Generic message — do not reveal whether username exists
            ModelState.AddModelError("", "Invalid username or password.");
            return View(model);
        }

        var user     = rows[0];
        bool isLocked = Convert.ToBoolean(user["IsLocked"]);

        if (isLocked)
        {
            ModelState.AddModelError("", "Account is locked due to too many failed attempts. Please contact the administrator.");
            return View(model);
        }

        string storedHash   = user["PasswordHash"]?.ToString() ?? "";
        string salt         = user["Salt"]?.ToString()         ?? "";
        int failedAttempts  = Convert.ToInt32(user["FailedLoginAttempts"]);

        // SECURE: PBKDF2 constant-time verification
        if (!PasswordHelper.VerifyPassword(model.Password, storedHash, salt))
        {
            failedAttempts++;
            bool lockNow = failedAttempts >= maxAttempts;

            _db.ExecuteNonQuery(
                "UPDATE Users SET FailedLoginAttempts = @Attempts, IsLocked = @Locked WHERE Id = @Id",
                new SqlParameter("@Attempts", failedAttempts),
                new SqlParameter("@Locked",   lockNow),
                new SqlParameter("@Id",       Convert.ToInt32(user["Id"])));

            if (lockNow)
                ModelState.AddModelError("", $"Account locked after {maxAttempts} failed attempts. Contact the administrator.");
            else
                ModelState.AddModelError("", $"Invalid username or password. {maxAttempts - failedAttempts} attempt(s) remaining before lockout.");

            return View(model);
        }

        // Reset counter on successful login
        _db.ExecuteNonQuery(
            "UPDATE Users SET FailedLoginAttempts = 0, IsLocked = 0 WHERE Id = @Id",
            new SqlParameter("@Id", Convert.ToInt32(user["Id"])));

        HttpContext.Session.SetInt32("UserId",   Convert.ToInt32(user["Id"]));
        HttpContext.Session.SetString("Username", user["Username"]?.ToString() ?? "");
        return RedirectToAction("Index", "Home");
    }

    // ---- LOGOUT --------------------------------------------------

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    // ---- CHANGE PASSWORD -----------------------------------------

    [HttpGet]
    [SessionAuthorize]
    public IActionResult ChangePassword() => View();

    [HttpPost]
    [SessionAuthorize]
    public IActionResult ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        int userId = HttpContext.Session.GetInt32("UserId")!.Value;

        var rows = _db.ExecuteQuery(
            "SELECT PasswordHash, Salt FROM Users WHERE Id = @Id",
            new SqlParameter("@Id", userId));

        if (rows.Count == 0) return RedirectToAction("Login");

        string storedHash = rows[0]["PasswordHash"]?.ToString() ?? "";
        string salt       = rows[0]["Salt"]?.ToString()         ?? "";

        // SECURE: Verify current password via PBKDF2 before allowing change
        if (!PasswordHelper.VerifyPassword(model.CurrentPassword, storedHash, salt))
        {
            ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
            return View(model);
        }

        // SECURE: Full policy validation including history check
        var errors = _policy.ValidatePassword(model.NewPassword, userId);
        foreach (var error in errors)
            ModelState.AddModelError("NewPassword", error);

        if (!ModelState.IsValid) return View(model);

        var (newHash, newSalt) = PasswordHelper.HashPassword(model.NewPassword);

        _db.ExecuteNonQuery(
            "UPDATE Users SET PasswordHash = @Hash, Salt = @Salt WHERE Id = @Id",
            new SqlParameter("@Hash", newHash),
            new SqlParameter("@Salt", newSalt),
            new SqlParameter("@Id",   userId));

        _db.ExecuteNonQuery(
            "INSERT INTO PasswordHistory (UserId, PasswordHash, Salt) VALUES (@UserId, @Hash, @Salt)",
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Hash",   newHash),
            new SqlParameter("@Salt",   newSalt));

        // Prune history — keep only the last N entries
        int historyCount = _config.GetSection("PasswordPolicy").GetValue<int>("PasswordHistoryCount");
        _db.ExecuteNonQuery(
            @"DELETE FROM PasswordHistory
              WHERE UserId = @UserId
                AND Id NOT IN (
                    SELECT TOP (@Count) Id FROM PasswordHistory
                    WHERE UserId = @UserId ORDER BY ChangedAt DESC
                )",
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Count",  historyCount));

        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction("Index", "Home");
    }

    // ---- FORGOT PASSWORD -----------------------------------------

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    public IActionResult ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // SECURE: Parameterized query
        var rows = _db.ExecuteQuery(
            "SELECT * FROM Users WHERE Email = @Email",
            new SqlParameter("@Email", model.Email));

        if (rows.Count > 0)
        {
            int userId = Convert.ToInt32(rows[0]["Id"]);

            // SECURE: Raw token sent to user; only the SHA-1 hash stored in DB
            string rawToken   = TokenService.GenerateToken();
            string tokenHash  = TokenService.HashToken(rawToken);

            // Invalidate any existing tokens for this user
            _db.ExecuteNonQuery(
                "UPDATE PasswordResetTokens SET IsUsed = 1 WHERE UserId = @UserId AND IsUsed = 0",
                new SqlParameter("@UserId", userId));

            _db.ExecuteNonQuery(
                "INSERT INTO PasswordResetTokens (UserId, TokenHash, ExpiresAt) VALUES (@UserId, @TokenHash, @ExpiresAt)",
                new SqlParameter("@UserId",    userId),
                new SqlParameter("@TokenHash", tokenHash),
                new SqlParameter("@ExpiresAt", DateTime.UtcNow.AddHours(1)));

            // Simulate email by displaying the link on screen
            TempData["ResetLink"] = Url.Action("ResetPassword", "Account",
                new { token = rawToken }, Request.Scheme);
        }

        // Identical response whether the email exists or not — prevents user enumeration
        TempData["Info"] = "If that email address is registered, a reset link has been sent.";
        return View();
    }

    // ---- RESET PASSWORD ------------------------------------------

    [HttpGet]
    public IActionResult ResetPassword(string token)
        => View(new ResetPasswordViewModel { Token = token });

    [HttpPost]
    public IActionResult ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // SECURE: Hash the submitted token before DB lookup
        string tokenHash = TokenService.HashToken(model.Token);

        var rows = _db.ExecuteQuery(
            "SELECT * FROM PasswordResetTokens WHERE TokenHash = @TokenHash AND IsUsed = 0 AND ExpiresAt > @Now",
            new SqlParameter("@TokenHash", tokenHash),
            new SqlParameter("@Now",       DateTime.UtcNow));

        if (rows.Count == 0)
        {
            ModelState.AddModelError("", "Invalid or expired reset token.");
            return View(model);
        }

        int userId  = Convert.ToInt32(rows[0]["UserId"]);
        int tokenId = Convert.ToInt32(rows[0]["Id"]);

        // SECURE: Validate new password against full policy
        var errors = _policy.ValidatePassword(model.NewPassword, userId);
        foreach (var error in errors)
            ModelState.AddModelError("NewPassword", error);

        if (!ModelState.IsValid) return View(model);

        var (hash, salt) = PasswordHelper.HashPassword(model.NewPassword);

        _db.ExecuteNonQuery(
            "UPDATE Users SET PasswordHash = @Hash, Salt = @Salt, FailedLoginAttempts = 0, IsLocked = 0 WHERE Id = @Id",
            new SqlParameter("@Hash", hash),
            new SqlParameter("@Salt", salt),
            new SqlParameter("@Id",   userId));

        _db.ExecuteNonQuery(
            "UPDATE PasswordResetTokens SET IsUsed = 1 WHERE Id = @Id",
            new SqlParameter("@Id", tokenId));

        _db.ExecuteNonQuery(
            "INSERT INTO PasswordHistory (UserId, PasswordHash, Salt) VALUES (@UserId, @Hash, @Salt)",
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Hash",   hash),
            new SqlParameter("@Salt",   salt));

        TempData["Success"] = "Password reset successful. Please login with your new password.";
        return RedirectToAction("Login");
    }
}
