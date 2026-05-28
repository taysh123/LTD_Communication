# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LTD_Communication is a university cybersecurity course project demonstrating vulnerable vs. secure ASP.NET Core MVC web applications for a fictional ISP company.

## Structure

```
/VulnerableVersion/   — ASP.NET Core MVC app with intentional SQL Injection + XSS vulnerabilities
/SecureVersion/       — Hardened version of the same app
/DatabaseScripts/     — SQL Server Express schema + seed scripts
/Documentation/       — Markdown documentation (PDF-style)
```

## Build & Run

Both versions are standard ASP.NET Core 8 MVC projects.

```powershell
# From either version folder:
dotnet restore
dotnet build
dotnet run
```

Database: SQL Server Express (LocalDB). Connection string in `appsettings.json`:
```
"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=LTD_Communication;Trusted_Connection=True;"
```

Apply schema before first run:
```powershell
# Run scripts in order:
# 1. DatabaseScripts/01_Schema.sql
# 2. DatabaseScripts/02_SeedData.sql
```

EF Core migrations are NOT used — raw SQL scripts are the source of truth.

## Architecture

Both versions share the same MVC layout:

- **Controllers**: `AccountController` (Register, Login, ChangePassword, ForgotPassword), `CustomerController` (Add/List customers), `HomeController`
- **Models**: `User`, `Customer`, `InternetPackage`, `Sector` — plain C# classes, no ORM
- **Views**: Razor `.cshtml` with Bootstrap 5
- **Data access**: `DbHelper` / `SqlHelper` static helper using `System.Data.SqlClient` — no EF Core
- **Password hashing**: PBKDF2 (SecureVersion) vs plain-text (VulnerableVersion)
- **Policy config**: `appsettings.json` → `PasswordPolicy` section (min length, complexity, history count, max attempts)

## Key Security Contrasts

| Feature | VulnerableVersion | SecureVersion |
|---|---|---|
| SQL queries | String concatenation | Parameterized (`SqlParameter`) |
| XSS | Raw `@Html.Raw(...)` | Auto-encoded Razor `@...` |
| Passwords | Plain-text | PBKDF2 + salt |
| Login lockout | None | Counter in `Users` table |
| Token hashing | None | SHA-256 |

## Password Policy (appsettings.json)

```json
"PasswordPolicy": {
  "MinLength": 10,
  "RequireUppercase": true,
  "RequireLowercase": true,
  "RequireDigit": true,
  "RequireSpecialChar": true,
  "PasswordHistoryCount": 3,
  "MaxLoginAttempts": 3
}
```

## Database Tables

`Users`, `Customers`, `InternetPackages`, `Sectors`, `PasswordHistory`, `PasswordResetTokens`
