using LTD_Communication.Vulnerable.Filters;
using LTD_Communication.Vulnerable.Helpers;
using LTD_Communication.Vulnerable.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LTD_Communication.Vulnerable.Controllers;

public class AccountController : Controller
{
    private readonly DbHelper _db;

    public AccountController(DbHelper db)
    {
        _db = db;
    }

    // ---- REGISTER ------------------------------------------------

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public IActionResult Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // VULNERABLE: SQL Injection via string concatenation
        // Attacker input in Username/Email can alter the query structure
        var exists = _db.ExecuteScalar(
            $"SELECT COUNT(*) FROM Users WHERE Username = '{model.Username}' OR Email = '{model.Email}'");

        if (Convert.ToInt32(exists) > 0)
        {
            ModelState.AddModelError("", "Username or email already exists.");
            return View(model);
        }

        // VULNERABLE: Plain-text password — no hashing
        string passwordHash = PasswordHelper.HashPassword(model.Password);

        _db.ExecuteNonQuery(
            $"INSERT INTO Users (Username, Email, PasswordHash) " +
            $"VALUES ('{model.Username}', '{model.Email}', '{passwordHash}')");

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

        // VULNERABLE: SQL Injection
        // Attack payload — Username: ' OR '1'='1' --
        // Resulting SQL: SELECT * FROM Users WHERE Username = '' OR '1'='1' --' AND PasswordHash = 'x'
        // The -- comments out the password check, returning all rows → login bypassed
        string sql = "SELECT * FROM Users WHERE Username = '" + model.Username +
                     "' AND PasswordHash = '" + model.Password + "'";

        var rows = _db.ExecuteQuery(sql);

        if (rows.Count > 0)
        {
            var user = rows[0];
            HttpContext.Session.SetInt32("UserId", Convert.ToInt32(user["Id"]));
            HttpContext.Session.SetString("Username", user["Username"]?.ToString() ?? "");
            return RedirectToAction("Index", "Home");
        }

        // VULNERABLE: No account lockout — unlimited brute-force attempts allowed
        ModelState.AddModelError("", "Invalid username or password.");
        return View(model);
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

        // VULNERABLE: String concatenation — SQL Injection possible in CurrentPassword
        var rows = _db.ExecuteQuery(
            $"SELECT * FROM Users WHERE Id = {userId} AND PasswordHash = '{model.CurrentPassword}'");

        if (rows.Count == 0)
        {
            ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
            return View(model);
        }

        // VULNERABLE: No password policy enforcement
        // VULNERABLE: No history check — can reuse the same password forever
        // VULNERABLE: Plain-text storage
        _db.ExecuteNonQuery(
            $"UPDATE Users SET PasswordHash = '{model.NewPassword}' WHERE Id = {userId}");

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

        // VULNERABLE: SQL Injection via email field
        var rows = _db.ExecuteQuery(
            $"SELECT * FROM Users WHERE Email = '{model.Email}'");

        if (rows.Count > 0)
        {
            var user = rows[0];
            int userId = Convert.ToInt32(user["Id"]);

            // VULNERABLE: Token is a plain Guid — not hashed before storage
            // Anyone who can read the DB gets a working reset token
            string token = Guid.NewGuid().ToString("N");

            _db.ExecuteNonQuery(
                $"INSERT INTO PasswordResetTokens (UserId, TokenHash, ExpiresAt) " +
                $"VALUES ({userId}, '{token}', '{DateTime.UtcNow.AddHours(1):yyyy-MM-dd HH:mm:ss}')");

            // Simulate email by rendering the link on screen
            TempData["ResetLink"] = Url.Action("ResetPassword", "Account",
                new { token }, Request.Scheme);
        }

        TempData["Info"] = "If the email exists, a password reset link has been generated.";
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

        // VULNERABLE: Token compared directly — no hashing
        // VULNERABLE: SQL Injection in token field
        var rows = _db.ExecuteQuery(
            $"SELECT * FROM PasswordResetTokens " +
            $"WHERE TokenHash = '{model.Token}' AND IsUsed = 0 " +
            $"AND ExpiresAt > '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}'");

        if (rows.Count == 0)
        {
            ModelState.AddModelError("", "Invalid or expired reset token.");
            return View(model);
        }

        int userId  = Convert.ToInt32(rows[0]["UserId"]);
        int tokenId = Convert.ToInt32(rows[0]["Id"]);

        // VULNERABLE: No password policy, plain-text storage
        _db.ExecuteNonQuery(
            $"UPDATE Users SET PasswordHash = '{model.NewPassword}' WHERE Id = {userId}");
        _db.ExecuteNonQuery(
            $"UPDATE PasswordResetTokens SET IsUsed = 1 WHERE Id = {tokenId}");

        TempData["Success"] = "Password reset successful. Please login.";
        return RedirectToAction("Login");
    }
}
