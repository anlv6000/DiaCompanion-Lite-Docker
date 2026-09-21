USE DiaCompanion;
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    /* ============================================================
       1. Kiểm tra cột Role có tồn tại không
       ============================================================ */

    IF COL_LENGTH(N'dbo.Users', N'Role') IS NULL
    BEGIN
        THROW 50000,
            N'Cột dbo.Users.Role không tồn tại hoặc đã được migration.',
            1;
    END;


    /* ============================================================
       2. Kiểm tra giá trị Role hiện tại
       ============================================================ */

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Users
        WHERE [Role] IS NULL
           OR [Role] NOT IN (0, 1, 2, 3, 4)
    )
    BEGIN
        THROW 50001,
            N'Users đang có Role NULL hoặc ngoài phạm vi 0,1,2,3,4.',
            1;
    END;


    /* ============================================================
       3. Không cho chạy nếu bảng mới đã tồn tại
       ============================================================ */

    IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
    BEGIN
        THROW 50002,
            N'Bảng dbo.Roles đã tồn tại.',
            1;
    END;

    IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NOT NULL
    BEGIN
        THROW 50003,
            N'Bảng dbo.UserRoles đã tồn tại.',
            1;
    END;


    /* ============================================================
       4. Tạo bảng Roles
       ============================================================ */

    CREATE TABLE dbo.Roles
    (
        Id              TINYINT        NOT NULL,
        Name            VARCHAR(50)    NOT NULL,
        DisplayName     NVARCHAR(100)  NOT NULL,
        Description     NVARCHAR(300)  NULL,
        IsActive        BIT            NOT NULL
            CONSTRAINT DF_Roles_IsActive DEFAULT 1,
        CreatedAt       DATETIME2      NOT NULL
            CONSTRAINT DF_Roles_CreatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_Roles
            PRIMARY KEY (Id),

        CONSTRAINT UQ_Roles_Name
            UNIQUE (Name),

        CONSTRAINT CK_Roles_Id
            CHECK (Id BETWEEN 0 AND 3)
    );


    /* ============================================================
       5. Thêm dữ liệu role
       ============================================================ */

    INSERT INTO dbo.Roles
    (
        Id,
        Name,
        DisplayName,
        Description,
        IsActive
    )
    VALUES
        (
            0,
            'Admin',
            N'Quản trị viên',
            N'Quản trị và cấu hình toàn bộ hệ thống',
            1
        ),
        (
            1,
            'Doctor',
            N'Bác sĩ',
            N'Khám bệnh, chẩn đoán và kết luận hồ sơ',
            1
        ),
        (
            2,
            'Receptionist',
            N'Lễ tân',
            N'Tiếp nhận bệnh nhân và tạo lượt khám',
            1
        ),
        (
            3,
            'Patient',
            N'Bệnh nhân',
            N'Sử dụng ứng dụng dành cho bệnh nhân',
            1
        );


    /* ============================================================
       6. Tạo bảng UserRoles
       ============================================================ */

    CREATE TABLE dbo.UserRoles
    (
        UserId          INT         NOT NULL,
        RoleId          TINYINT     NOT NULL,

        AssignedAt      DATETIME2   NOT NULL
            CONSTRAINT DF_UserRoles_AssignedAt
            DEFAULT SYSUTCDATETIME(),

        AssignedBy      INT         NULL,

        IsActive        BIT         NOT NULL
            CONSTRAINT DF_UserRoles_IsActive
            DEFAULT 1,

        CONSTRAINT PK_UserRoles
            PRIMARY KEY (UserId, RoleId),

        CONSTRAINT FK_UserRoles_User
            FOREIGN KEY (UserId)
            REFERENCES dbo.Users(Id)
            ON DELETE NO ACTION,

        CONSTRAINT FK_UserRoles_Role
            FOREIGN KEY (RoleId)
            REFERENCES dbo.Roles(Id)
            ON DELETE NO ACTION,

        CONSTRAINT FK_UserRoles_AssignedBy
            FOREIGN KEY (AssignedBy)
            REFERENCES dbo.Users(Id)
            ON DELETE NO ACTION
    );


    /* ============================================================
       7. Chuyển dữ liệu Users.Role sang UserRoles

       Quy đổi:
       0 -> Admin
       1 -> Doctor
       2 -> Receptionist
       3 -> Patient
       4 -> Receptionist
       ============================================================ */

    INSERT INTO dbo.UserRoles
    (
        UserId,
        RoleId,
        AssignedAt,
        AssignedBy,
        IsActive
    )
    SELECT
        u.Id,
        CASE
            WHEN u.[Role] = 4 THEN CAST(2 AS TINYINT)
            ELSE CAST(u.[Role] AS TINYINT)
        END,
        SYSUTCDATETIME(),
        NULL,
        1
    FROM dbo.Users AS u;


    /* ============================================================
       8. Kiểm tra số lượng dữ liệu đã chuyển
       ============================================================ */

    DECLARE @UserCount INT;
    DECLARE @UserRoleCount INT;

    SELECT @UserCount = COUNT(*)
    FROM dbo.Users;

    SELECT @UserRoleCount = COUNT(*)
    FROM dbo.UserRoles;

    IF @UserCount <> @UserRoleCount
    BEGIN
        THROW 50004,
            N'Số lượng UserRoles không khớp với số lượng Users.',
            1;
    END;


    /* ============================================================
       9. Kiểm tra user nào chưa có role
       ============================================================ */

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Users AS u
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.UserRoles AS ur
            WHERE ur.UserId = u.Id
              AND ur.IsActive = 1
        )
    )
    BEGIN
        THROW 50005,
            N'Có người dùng chưa được thêm vào UserRoles.',
            1;
    END;


    /* ============================================================
       10. Xóa CHECK constraint liên quan đến Users.Role
       ============================================================ */

    DECLARE @DropCheckSql NVARCHAR(MAX) = N'';

    SELECT
        @DropCheckSql =
            @DropCheckSql
            + N'ALTER TABLE dbo.Users DROP CONSTRAINT '
            + QUOTENAME(cc.name)
            + N';'
            + CHAR(13)
            + CHAR(10)
    FROM sys.check_constraints AS cc
    INNER JOIN sys.tables AS t
        ON t.object_id = cc.parent_object_id
    INNER JOIN sys.schemas AS s
        ON s.schema_id = t.schema_id
    WHERE s.name = N'dbo'
      AND t.name = N'Users'
      AND
      (
          cc.name = N'CK_Users_Role'
          OR cc.definition LIKE N'%Role%'
      );

    IF LEN(@DropCheckSql) > 0
    BEGIN
        EXEC sys.sp_executesql @DropCheckSql;
    END;


    /* ============================================================
       11. Xóa DEFAULT constraint của Users.Role nếu có
       ============================================================ */

    DECLARE @RoleDefaultConstraint SYSNAME;
    DECLARE @DropDefaultSql NVARCHAR(MAX);

    SELECT
        @RoleDefaultConstraint = dc.name
    FROM sys.default_constraints AS dc
    INNER JOIN sys.columns AS c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    INNER JOIN sys.tables AS t
        ON t.object_id = c.object_id
    INNER JOIN sys.schemas AS s
        ON s.schema_id = t.schema_id
    WHERE s.name = N'dbo'
      AND t.name = N'Users'
      AND c.name = N'Role';

    IF @RoleDefaultConstraint IS NOT NULL
    BEGIN
        SET @DropDefaultSql =
            N'ALTER TABLE dbo.Users DROP CONSTRAINT '
            + QUOTENAME(@RoleDefaultConstraint)
            + N';';

        EXEC sys.sp_executesql @DropDefaultSql;
    END;


    /* ============================================================
       12. Xóa index liên quan trực tiếp đến Users.Role nếu có
       ============================================================ */

    DECLARE @DropIndexSql NVARCHAR(MAX) = N'';

    SELECT
        @DropIndexSql =
            @DropIndexSql
            + N'DROP INDEX '
            + QUOTENAME(i.name)
            + N' ON dbo.Users;'
            + CHAR(13)
            + CHAR(10)
    FROM sys.indexes AS i
    INNER JOIN sys.index_columns AS ic
        ON ic.object_id = i.object_id
       AND ic.index_id = i.index_id
    INNER JOIN sys.columns AS c
        ON c.object_id = ic.object_id
       AND c.column_id = ic.column_id
    WHERE i.object_id = OBJECT_ID(N'dbo.Users')
      AND c.name = N'Role'
      AND i.is_primary_key = 0
      AND i.is_unique_constraint = 0;

    IF LEN(@DropIndexSql) > 0
    BEGIN
        EXEC sys.sp_executesql @DropIndexSql;
    END;


    /* ============================================================
       13. Xóa cột Role khỏi Users
       ============================================================ */

    ALTER TABLE dbo.Users
    DROP COLUMN [Role];


    /* ============================================================
       14. Tạo index cho UserRoles
       ============================================================ */

    CREATE INDEX IX_UserRoles_Role
    ON dbo.UserRoles
    (
        RoleId,
        IsActive,
        UserId
    );

    CREATE INDEX IX_UserRoles_UserActive
    ON dbo.UserRoles
    (
        UserId,
        IsActive
    )
    INCLUDE (RoleId);


    COMMIT TRANSACTION;

    PRINT N'Migration Roles và UserRoles thành công.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorLine INT = ERROR_LINE();
    DECLARE @ErrorNumber INT = ERROR_NUMBER();

    PRINT N'Migration thất bại.';
    PRINT N'Error number: ' + CAST(@ErrorNumber AS NVARCHAR(20));
    PRINT N'Error line: ' + CAST(@ErrorLine AS NVARCHAR(20));
    PRINT N'Error message: ' + @ErrorMessage;

    THROW;
END CATCH;
GO










CREATE TABLE dbo.MedicalRecords
(
    Id INT IDENTITY(1,1) NOT NULL
        CONSTRAINT PK_MedicalRecords PRIMARY KEY,

    PatientId INT NOT NULL,

    RecordCode NVARCHAR(30) NOT NULL,

    CreatedAt DATETIME2(7) NOT NULL
        CONSTRAINT DF_MedicalRecords_CreatedAt
        DEFAULT SYSUTCDATETIME(),

    CreatedByUserId INT NULL,

    UpdatedAt DATETIME2(7) NULL,

    UpdatedByUserId INT NULL,

    IsVoided BIT NOT NULL
        CONSTRAINT DF_MedicalRecords_IsVoided
        DEFAULT 0,

    VoidedAt DATETIME2(7) NULL,

    VoidedByUserId INT NULL,

    VoidReason NVARCHAR(500) NULL,

    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT UQ_MedicalRecords_RecordCode
        UNIQUE (RecordCode),

    CONSTRAINT FK_MedicalRecords_Patients
        FOREIGN KEY (PatientId)
        REFERENCES dbo.Patients(Id),

    CONSTRAINT FK_MedicalRecords_CreatedByUser
        FOREIGN KEY (CreatedByUserId)
        REFERENCES dbo.Users(Id),

    CONSTRAINT FK_MedicalRecords_UpdatedByUser
        FOREIGN KEY (UpdatedByUserId)
        REFERENCES dbo.Users(Id),

    CONSTRAINT FK_MedicalRecords_VoidedByUser
        FOREIGN KEY (VoidedByUserId)
        REFERENCES dbo.Users(Id)
);
GO

CREATE UNIQUE INDEX UX_MedicalRecords_ActivePatient
ON dbo.MedicalRecords(PatientId)
WHERE IsVoided = 0;
GO



INSERT INTO dbo.MedicalRecords
(
    PatientId,
    RecordCode,
    CreatedAt,
    IsVoided
)
SELECT
    p.Id,
    CONCAT(N'MR-', p.Code),
    SYSUTCDATETIME(),
    0
FROM dbo.Patients AS p
WHERE p.IsVoided = 0
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.MedicalRecords AS mr
      WHERE mr.PatientId = p.Id
        AND mr.IsVoided = 0
  );
GO


ALTER TABLE dbo.Visits
ADD MedicalRecordId INT NULL;
GO

SELECT
    c.name,
    TYPE_NAME(c.user_type_id) AS DataType
FROM sys.columns AS c
WHERE c.object_id = OBJECT_ID(N'dbo.Visits')
  AND c.name = N'MedicalRecordId';
GO

UPDATE v
SET v.MedicalRecordId = mr.Id
FROM dbo.Visits AS v
INNER JOIN dbo.MedicalRecords AS mr
    ON mr.PatientId = v.PatientId
   AND mr.IsVoided = 0
WHERE v.MedicalRecordId IS NULL;
GO




ALTER TABLE dbo.Visits
ALTER COLUMN MedicalRecordId INT NOT NULL;
GO

ALTER TABLE dbo.Visits
ADD CONSTRAINT FK_Visits_MedicalRecords
    FOREIGN KEY (MedicalRecordId)
    REFERENCES dbo.MedicalRecords(Id);
GO

CREATE INDEX IX_Visits_MedicalRecordId
ON dbo.Visits(MedicalRecordId);
GO



 EXEC sp_rename 'dbo.Visits', 'MedicalVisits';