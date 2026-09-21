/*
    DiaCompanion - chuyển trạng thái tài khoản sang UserRoles.IsActive.

    Application không còn đọc/ghi Users.IsActive.
    Cột Users.IsActive có thể giữ lại để tương thích schema cũ, nhưng không còn
    tham gia login, authorization, danh sách staff hay khóa/mở tài khoản.

    Quan trọng: Email/Phone bây giờ thuộc duy nhất một User bất kể role đang khóa.
    Vì vậy unique index không được phụ thuộc Users.IsActive nữa.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

/* 1. Chặn migration nếu dữ liệu cũ đang có Phone trùng giữa nhiều User. */
IF EXISTS (
    SELECT Phone
    FROM dbo.Users
    WHERE Phone IS NOT NULL
    GROUP BY Phone
    HAVING COUNT(*) > 1
)
BEGIN
    SELECT Phone, COUNT(*) AS DuplicateCount
    FROM dbo.Users
    WHERE Phone IS NOT NULL
    GROUP BY Phone
    HAVING COUNT(*) > 1;

    ROLLBACK TRANSACTION;
    THROW 50001, N'Users đang có Phone trùng. Cần hợp nhất/xử lý các User trùng trước khi đổi unique index.', 1;
END;

/* 2. Chặn migration nếu dữ liệu cũ đang có Email trùng giữa nhiều User. */
IF EXISTS (
    SELECT Email
    FROM dbo.Users
    WHERE Email IS NOT NULL
    GROUP BY Email
    HAVING COUNT(*) > 1
)
BEGIN
    SELECT Email, COUNT(*) AS DuplicateCount
    FROM dbo.Users
    WHERE Email IS NOT NULL
    GROUP BY Email
    HAVING COUNT(*) > 1;

    ROLLBACK TRANSACTION;
    THROW 50002, N'Users đang có Email trùng. Cần hợp nhất/xử lý các User trùng trước khi đổi unique index.', 1;
END;

/* 3. Xóa unique index một cột cũ trên Phone/Email, bất kể tên index cũ là gì. */
DECLARE @dropSql nvarchar(max) = N'';

SELECT @dropSql = @dropSql
    + N'DROP INDEX ' + QUOTENAME(i.name) + N' ON dbo.Users;' + CHAR(13)
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID(N'dbo.Users')
  AND i.is_unique = 1
  AND i.is_primary_key = 0
  AND (
        (
            SELECT COUNT(*)
            FROM sys.index_columns ic
            WHERE ic.object_id = i.object_id
              AND ic.index_id = i.index_id
              AND ic.key_ordinal > 0
        ) = 1
      )
  AND EXISTS (
        SELECT 1
        FROM sys.index_columns ic
        JOIN sys.columns c
          ON c.object_id = ic.object_id
         AND c.column_id = ic.column_id
        WHERE ic.object_id = i.object_id
          AND ic.index_id = i.index_id
          AND ic.key_ordinal = 1
          AND c.name IN (N'Phone', N'Email')
      );

IF LEN(@dropSql) > 0
    EXEC sys.sp_executesql @dropSql;

/* 4. Tạo unique index mới: chỉ bỏ qua NULL, không phụ thuộc Users.IsActive. */
CREATE UNIQUE INDEX UX_Users_Phone
    ON dbo.Users(Phone)
    WHERE Phone IS NOT NULL;

CREATE UNIQUE INDEX UX_Users_Email
    ON dbo.Users(Email)
    WHERE Email IS NOT NULL;

COMMIT TRANSACTION;
GO

/* Kiểm tra nhanh sau migration. */
SELECT TOP (50)
    u.Id,
    u.Email,
    u.Phone,
    ur.RoleId,
    r.Name AS RoleName,
    ur.IsActive AS UserRoleIsActive,
    r.IsActive AS RoleIsActive
FROM dbo.Users u
LEFT JOIN dbo.UserRoles ur ON ur.UserId = u.Id
LEFT JOIN dbo.Roles r ON r.Id = ur.RoleId
ORDER BY u.Id, r.Name;
GO
