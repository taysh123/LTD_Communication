-- ============================================================
-- LTD_Communication — Database Schema
-- Run this script against SQL Server Express / LocalDB
--
-- Usage:
--   For VulnerableVersion: database = LTD_Communication_Vulnerable
--   For SecureVersion:     database = LTD_Communication_Secure
--
-- Run in SSMS or sqlcmd:
--   sqlcmd -S "(localdb)\mssqllocaldb" -Q "CREATE DATABASE LTD_Communication_Vulnerable"
--   sqlcmd -S "(localdb)\mssqllocaldb" -d LTD_Communication_Vulnerable -i 01_Schema.sql
-- ============================================================

-- Drop tables in reverse FK order (safe re-run)
IF OBJECT_ID('dbo.PasswordResetTokens', 'U') IS NOT NULL DROP TABLE dbo.PasswordResetTokens;
IF OBJECT_ID('dbo.PasswordHistory',     'U') IS NOT NULL DROP TABLE dbo.PasswordHistory;
IF OBJECT_ID('dbo.Customers',           'U') IS NOT NULL DROP TABLE dbo.Customers;
IF OBJECT_ID('dbo.InternetPackages',    'U') IS NOT NULL DROP TABLE dbo.InternetPackages;
IF OBJECT_ID('dbo.Sectors',             'U') IS NOT NULL DROP TABLE dbo.Sectors;
IF OBJECT_ID('dbo.Users',               'U') IS NOT NULL DROP TABLE dbo.Users;
GO

-- ---- Users -----------------------------------------------
CREATE TABLE dbo.Users (
    Id                  INT            IDENTITY(1,1) PRIMARY KEY,
    Username            NVARCHAR(100)  NOT NULL,
    Email               NVARCHAR(200)  NOT NULL,
    PasswordHash        NVARCHAR(500)  NOT NULL,
    Salt                NVARCHAR(200)  NULL,          -- NULL for VulnerableVersion (plain-text)
    FailedLoginAttempts INT            NOT NULL DEFAULT 0,
    IsLocked            BIT            NOT NULL DEFAULT 0,
    CreatedAt           DATETIME       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT UQ_Users_Email    UNIQUE (Email)
);
GO

-- ---- Password history (SecureVersion only) ---------------
CREATE TABLE dbo.PasswordHistory (
    Id           INT            IDENTITY(1,1) PRIMARY KEY,
    UserId       INT            NOT NULL,
    PasswordHash NVARCHAR(500)  NOT NULL,
    Salt         NVARCHAR(200)  NOT NULL,
    ChangedAt    DATETIME       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_PasswordHistory_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
);
GO

-- ---- Password reset tokens --------------------------------
CREATE TABLE dbo.PasswordResetTokens (
    Id        INT            IDENTITY(1,1) PRIMARY KEY,
    UserId    INT            NOT NULL,
    TokenHash NVARCHAR(500)  NOT NULL,
    ExpiresAt DATETIME       NOT NULL,
    IsUsed    BIT            NOT NULL DEFAULT 0,
    CreatedAt DATETIME       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_PasswordResetTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
);
GO

-- ---- Sectors ----------------------------------------------
CREATE TABLE dbo.Sectors (
    Id          INT            IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(100)  NOT NULL,
    Description NVARCHAR(500)  NULL
);
GO

-- ---- Internet packages ------------------------------------
CREATE TABLE dbo.InternetPackages (
    Id          INT             IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(100)   NOT NULL,
    Speed       NVARCHAR(50)    NOT NULL,
    Price       DECIMAL(10, 2)  NOT NULL,
    Description NVARCHAR(500)   NULL
);
GO

-- ---- Customers --------------------------------------------
CREATE TABLE dbo.Customers (
    Id        INT            IDENTITY(1,1) PRIMARY KEY,
    FullName  NVARCHAR(200)  NOT NULL,
    Email     NVARCHAR(200)  NOT NULL,
    Phone     NVARCHAR(20)   NULL,
    Address   NVARCHAR(500)  NULL,
    SectorId  INT            NULL,
    PackageId INT            NULL,
    CreatedBy INT            NULL,
    CreatedAt DATETIME       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Customers_Sectors          FOREIGN KEY (SectorId)  REFERENCES dbo.Sectors(Id),
    CONSTRAINT FK_Customers_InternetPackages FOREIGN KEY (PackageId) REFERENCES dbo.InternetPackages(Id),
    CONSTRAINT FK_Customers_Users            FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(Id)
);
GO

PRINT 'Schema created successfully.';
GO
