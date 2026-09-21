-- ============================================================
-- 1. Add IsVoided
-- ============================================================
IF COL_LENGTH('dbo.Users', 'IsVoided') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD IsVoided bit NOT NULL
        CONSTRAINT DF_Users_IsVoided DEFAULT 0;
END
GO


-- ============================================================
-- 2. Rebuild unique Phone index
-- ============================================================
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_Users_Phone'
      AND object_id = OBJECT_ID('dbo.Users')
)
BEGIN
    DROP INDEX UX_Users_Phone
    ON dbo.Users;
END
GO

CREATE UNIQUE INDEX UX_Users_Phone
ON dbo.Users(Phone)
WHERE Phone IS NOT NULL
  AND IsVoided = 0;
GO


-- ============================================================
-- 3. Rebuild unique Email index
-- ============================================================
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_Users_Email'
      AND object_id = OBJECT_ID('dbo.Users')
)
BEGIN
    DROP INDEX UX_Users_Email
    ON dbo.Users;
END
GO

CREATE UNIQUE INDEX UX_Users_Email
ON dbo.Users(Email)
WHERE Email IS NOT NULL
  AND IsVoided = 0;
GO