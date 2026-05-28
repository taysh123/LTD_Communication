-- ============================================================
-- LTD_Communication — Seed Data
-- Run AFTER 01_Schema.sql against the target database.
--
-- This seed creates:
--   - 3 Sectors
--   - 4 Internet Packages
--   - 1 admin user with PLAIN-TEXT password (VulnerableVersion demo)
--
-- For SecureVersion: run this script then start the app —
-- Program.cs auto-seeds a PBKDF2-hashed admin user.
-- ============================================================

-- Sectors
IF NOT EXISTS (SELECT 1 FROM dbo.Sectors WHERE Name = 'Residential')
    INSERT INTO dbo.Sectors (Name, Description)
    VALUES ('Residential', 'Home internet services for individual customers');

IF NOT EXISTS (SELECT 1 FROM dbo.Sectors WHERE Name = 'Commercial')
    INSERT INTO dbo.Sectors (Name, Description)
    VALUES ('Commercial', 'Business internet solutions for SMEs and enterprises');

IF NOT EXISTS (SELECT 1 FROM dbo.Sectors WHERE Name = 'Industrial')
    INSERT INTO dbo.Sectors (Name, Description)
    VALUES ('Industrial', 'High-bandwidth connectivity for industrial facilities');
GO

-- Internet packages
IF NOT EXISTS (SELECT 1 FROM dbo.InternetPackages WHERE Name = 'Basic')
    INSERT INTO dbo.InternetPackages (Name, Speed, Price, Description)
    VALUES ('Basic', '50 Mbps', 29.99, 'Suitable for light browsing and email');

IF NOT EXISTS (SELECT 1 FROM dbo.InternetPackages WHERE Name = 'Standard')
    INSERT INTO dbo.InternetPackages (Name, Speed, Price, Description)
    VALUES ('Standard', '100 Mbps', 49.99, 'Great for HD streaming and remote work');

IF NOT EXISTS (SELECT 1 FROM dbo.InternetPackages WHERE Name = 'Premium')
    INSERT INTO dbo.InternetPackages (Name, Speed, Price, Description)
    VALUES ('Premium', '500 Mbps', 79.99, 'Perfect for multiple devices and 4K streaming');

IF NOT EXISTS (SELECT 1 FROM dbo.InternetPackages WHERE Name = 'Ultra')
    INSERT INTO dbo.InternetPackages (Name, Speed, Price, Description)
    VALUES ('Ultra', '1 Gbps', 119.99, 'Maximum speed for power users and businesses');
GO

-- Admin user — PLAIN TEXT password for VulnerableVersion demonstration
-- Password: admin123  (stored as-is, no hashing — intentional vulnerability)
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = 'admin')
    INSERT INTO dbo.Users (Username, Email, PasswordHash, Salt)
    VALUES ('admin', 'admin@ltd-communication.com', 'admin123', NULL);
GO

PRINT 'Seed data inserted successfully.';
GO
