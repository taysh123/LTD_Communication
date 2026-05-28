# LTD Communication — Cybersecurity Course Project

**Vulnerable vs. Secure ASP.NET Core 8 MVC Web Application**

A university Cybersecurity course project demonstrating four classic web application
vulnerabilities in a realistic ISP management system — and the corresponding OWASP-recommended
mitigations in a hardened companion application.

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Tech Stack](#tech-stack)
3. [Vulnerabilities Demonstrated](#vulnerabilities-demonstrated)
4. [Security Protections Implemented](#security-protections-implemented)
5. [Project Structure](#project-structure)
6. [Prerequisites](#prerequisites)
7. [Database Setup](#database-setup)
8. [Running the Applications](#running-the-applications)
9. [Default Credentials](#default-credentials)
10. [Demonstrating the Attacks](#demonstrating-the-attacks)
11. [Demonstrating the Protections](#demonstrating-the-protections)
12. [Screenshots](#screenshots)
13. [Key File Reference](#key-file-reference)
14. [Educational Disclaimer](#educational-disclaimer)

---

## Project Overview

**Company (fictional):** LTD Communication — Internet Service Provider

| Version | Purpose |
|---|---|
| **VulnerableVersion** | Intentionally insecure — SQL Injection, Stored XSS, brute force, plain-text passwords |
| **SecureVersion** | OWASP-hardened — parameterized SQL, output encoding, account lockout, PBKDF2 hashing |

---

## Tech Stack

| Component | Technology |
|---|---|
| Framework | ASP.NET Core 8 MVC |
| Language | C# 12 |
| Database | SQL Server Express / LocalDB |
| ORM | None — raw ADO.NET via `Microsoft.Data.SqlClient` |
| UI | Bootstrap 5, Razor (.cshtml) |
| Password hashing | PBKDF2-SHA256, 100 000 iterations (SecureVersion only) |
| Auth | Cookie-based session (`ISession`) |

---

## Vulnerabilities Demonstrated

| # | Vulnerability | Location |
|---|---|---|
| 1 | **SQL Injection** — login bypass via `' OR '1'='1' --` | `VulnerableVersion/.../Helpers/DbHelper.cs` (string concatenation) |
| 2 | **Stored XSS** — `<script>` stored in DB, executed in browser | `VulnerableVersion/.../Views/Customer/List.cshtml` (`@Html.Raw`) |
| 3 | **Brute Force** — no login attempt limit | `VulnerableVersion/.../Controllers/AccountController.cs` |
| 4 | **Plain-text passwords** — stored as-is in `PasswordHash` column | `VulnerableVersion/.../Helpers/PasswordHelper.cs` |

---

## Security Protections Implemented

| # | Protection | Mechanism |
|---|---|---|
| 1 | **Parameterized SQL** | `SqlParameter[]` in `SecureVersion/.../Helpers/DbHelper.cs` |
| 2 | **Output encoding** | Razor auto-encodes `@expression` — no `@Html.Raw` |
| 3 | **Account lockout** | `FailedLoginAttempts` + `IsLocked` columns; locks after 3 failures |
| 4 | **PBKDF2 password hashing** | `Rfc2898DeriveBytes`, SHA-256, 100 000 iterations, random 256-bit salt |
| 5 | **Password policy** | Min length 10, uppercase, lowercase, digit, special char, dictionary check |
| 6 | **Password history** | Cannot reuse last 3 passwords |
| 7 | **Token hashing** | Password-reset tokens hashed with SHA-256 before DB storage |

---

## Project Structure

```
LTD_Communication/
├── VulnerableVersion/
│   └── LTD_Communication.Vulnerable/     ← ASP.NET Core 8 MVC (intentionally vulnerable)
├── SecureVersion/
│   └── LTD_Communication.Secure/         ← ASP.NET Core 8 MVC (OWASP-hardened)
├── DatabaseScripts/
│   ├── 01_Schema.sql                     ← Full table definitions
│   └── 02_SeedData.sql                   ← Sectors, packages, admin user (plain-text)
├── Documentation/
│   └── LTD_Communication_Documentation.md
├── Screenshots/                          ← 9 application demo screenshots
├── CLAUDE.md                             ← AI assistant guidance
└── README.md
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server Express with LocalDB  
  Install via: **Visual Studio Installer → Individual Components → SQL Server Express LocalDB**  
  Or standalone: [SQL Server Express download](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
- (Optional) [SQL Server Management Studio (SSMS)](https://aka.ms/ssmsfullsetup) — for inspecting the database directly

---

## Database Setup

Both versions use **separate databases** so they do not interfere with each other.

### Step 1 — Create the databases

```powershell
sqlcmd -S "(localdb)\mssqllocaldb" -Q "CREATE DATABASE LTD_Communication_Vulnerable"
sqlcmd -S "(localdb)\mssqllocaldb" -Q "CREATE DATABASE LTD_Communication_Secure"
```

### Step 2 — Apply the schema to both databases

```powershell
sqlcmd -S "(localdb)\mssqllocaldb" -d LTD_Communication_Vulnerable -i "DatabaseScripts\01_Schema.sql"
sqlcmd -S "(localdb)\mssqllocaldb" -d LTD_Communication_Secure     -i "DatabaseScripts\01_Schema.sql"
```

### Step 3 — Seed the VulnerableVersion database

```powershell
sqlcmd -S "(localdb)\mssqllocaldb" -d LTD_Communication_Vulnerable -i "DatabaseScripts\02_SeedData.sql"
```

> The **SecureVersion** seeds its own data (Sectors, Packages, hashed admin user)
> automatically when the app starts for the first time.

---

## Running the Applications

### VulnerableVersion

```powershell
cd "VulnerableVersion\LTD_Communication.Vulnerable"
dotnet run
```

Browse to: **http://localhost:5000**

### SecureVersion

```powershell
cd "SecureVersion\LTD_Communication.Secure"
dotnet run
```

Browse to: **http://localhost:5001** (or the port printed in the terminal)

> If both apps run simultaneously, edit `Properties/launchSettings.json` to use different ports.

---

## Default Credentials

| Version | Username | Password | Notes |
|---|---|---|---|
| VulnerableVersion | `admin` | `admin123` | Stored as plain text in the database |
| SecureVersion | `admin` | `Admin@12345!` | PBKDF2-hashed; auto-created on first run |

---

## Demonstrating the Attacks

### Attack 1 — SQL Injection Login Bypass

1. Open **VulnerableVersion** and navigate to **Login**
2. Enter in the **Username** field:
   ```
   ' OR '1'='1' --
   ```
3. Enter anything in the **Password** field (e.g. `xyz`)
4. Click **Login**

**Expected result:** Logged in as `admin` without knowing the password.

**Why it works:**
```sql
SELECT * FROM Users WHERE Username = '' OR '1'='1' --' AND PasswordHash = 'xyz'
```
`'1'='1'` is always TRUE; `--` comments out the password check.

---

### Attack 2 — Stored XSS

1. Log in to **VulnerableVersion** → **Add Customer**
2. Enter in **Full Name**:
   ```
   <script>alert('XSS Attack!')</script>
   ```
3. Fill in any valid email → **Add Customer**
4. Navigate to **Customer List**

**Expected result:** A JavaScript `alert()` popup fires.

**More dangerous payload for demonstration:**
```html
<script>document.title='HACKED by '+document.cookie</script>
```

---

### Attack 3 — Brute Force

On the **VulnerableVersion** Login page, attempt incorrect passwords repeatedly — there is no limit. An attacker can automate credential stuffing or dictionary attacks freely.

---

### Attack 4 — Plain-text Password Exposure

```sql
USE LTD_Communication_Vulnerable;
SELECT Username, PasswordHash, Salt FROM Users;
-- Result: admin | admin123 | NULL   ← immediately exploitable
```

---

## Demonstrating the Protections

### Protection 1 — SQL Injection Blocked

1. Open **SecureVersion** → **Login**
2. Enter the same payload: `' OR '1'='1' --`
3. Click **Login**

**Expected result:** "Invalid username or password." — the payload is bound as a string parameter, not concatenated.

---

### Protection 2 — XSS Blocked

1. Log in to **SecureVersion** → **Add Customer**
2. Enter `<script>alert('XSS')</script>` in **Full Name**
3. Navigate to **Customer List**

**Expected result:** The raw text is displayed as a literal string. Razor auto-encodes `<` → `&lt;`, `>` → `&gt;`. No popup fires.

---

### Protection 3 — Account Lockout

1. Navigate to **SecureVersion Login**
2. Enter `admin` with wrong passwords 3 times in a row
3. After the 3rd failure: *"Account locked after 3 failed attempts."*

The account remains locked until `IsLocked = 0` is restored in the database.

---

### Protection 4 — Password Policy

1. Navigate to **SecureVersion Register** (or **Change Password**)
2. Try a weak password such as `abc` or `password123`

Expected errors:
- *"Password must be at least 10 characters long."*
- *"Password must contain at least one uppercase letter (A-Z)."*
- *"Password is too common. Please choose a more unique password."*

---

### Protection 5 — Password History

1. Log in to **SecureVersion** → **Change Password**
2. Enter your current password, then enter the **same password** as the new password

**Expected:** *"Password cannot match any of your last 3 passwords."*

---

### Protection 6 — PBKDF2 Hashing vs. Plain Text

```sql
-- VulnerableVersion:
USE LTD_Communication_Vulnerable;
SELECT Username, PasswordHash, Salt FROM Users;
-- admin | admin123 | NULL   ← plain text

-- SecureVersion:
USE LTD_Communication_Secure;
SELECT Username, PasswordHash, Salt FROM Users;
-- admin | QpX8rNs...== | a7F+3mKp...==   ← PBKDF2 hash + random salt
```

---

## Screenshots

Application screenshots are in the `/Screenshots` folder:

| File | Description |
|---|---|
| `WhatsApp Image 2026-05-26 at 21.56.56.jpeg` | Screenshot 1 |
| `WhatsApp Image 2026-05-26 at 21.57.50.jpeg` | Screenshot 2 |
| `WhatsApp Image 2026-05-26 at 21.58.57.jpeg` | Screenshot 3 |
| `WhatsApp Image 2026-05-26 at 22.06.37.jpeg` | Screenshot 4 |
| `WhatsApp Image 2026-05-26 at 22.06.49.jpeg` | Screenshot 5 |
| `WhatsApp Image 2026-05-26 at 22.08.54.jpeg` | Screenshot 6 |
| `WhatsApp Image 2026-05-26 at 22.09.46.jpeg` | Screenshot 7 |
| `WhatsApp Image 2026-05-26 at 22.10.57.jpeg` | Screenshot 8 |
| `WhatsApp Image 2026-05-26 at 22.11.17.jpeg` | Screenshot 9 |

---

## Key File Reference

| File | Purpose |
|---|---|
| `VulnerableVersion/.../Helpers/DbHelper.cs` | SQL execution via string concatenation — SQL Injection vector |
| `VulnerableVersion/.../Controllers/AccountController.cs` | Vulnerable login, register, password flows |
| `VulnerableVersion/.../Views/Customer/List.cshtml` | `@Html.Raw()` → XSS vector |
| `SecureVersion/.../Helpers/DbHelper.cs` | Parameterized SQL via `SqlParameter[]` |
| `SecureVersion/.../Helpers/PasswordHelper.cs` | PBKDF2-SHA256 hashing + constant-time verification |
| `SecureVersion/.../Services/PasswordPolicyService.cs` | Config-driven policy (reads `appsettings.json`) |
| `SecureVersion/.../Services/TokenService.cs` | Generates and hashes password-reset tokens |
| `SecureVersion/.../Views/Customer/List.cshtml` | `@c.FullName` → auto-encoded, XSS blocked |
| `DatabaseScripts/01_Schema.sql` | Full table definitions (both databases) |
| `DatabaseScripts/02_SeedData.sql` | Sectors, packages, vulnerable admin user |

---

## Educational Disclaimer

This repository is an **educational project** created for a university Cybersecurity course.

The `VulnerableVersion` application contains **intentional security vulnerabilities** (SQL Injection, Stored XSS, missing account lockout, plain-text password storage). These vulnerabilities exist solely to demonstrate real-world attack scenarios in a controlled, local-only environment.

**Do not deploy the VulnerableVersion to any publicly accessible server.**  
**Do not use any techniques demonstrated here against systems you do not own.**

All demonstrations must be performed on a local development machine with only synthetic test data.


student 1 : tay shofer 211697107

student 2 : taisiya angel 209238013