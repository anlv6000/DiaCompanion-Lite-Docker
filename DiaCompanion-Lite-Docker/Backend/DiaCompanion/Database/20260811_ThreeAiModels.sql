/* ============================================================================
   DiaCompanion — chuyển quản lý AI từ 1 active model toàn hệ thống sang
   3 active model độc lập: DR, Lesion, Fractal.

   ModelType:
     1 = DR grading
     2 = Lesion segmentation
     3 = Fractal / vessel segmentation

   Tương thích dữ liệu cũ:
   - AiDiagnoses.ModelVersionId được giữ nguyên và từ nay là DR model.
   - LesionModelVersionId / FractalModelVersionId được bổ sung.
   - Với dữ liệu cũ, hai cột mới được backfill bằng ModelVersionId vì backend cũ
     đã dùng cùng một ModelVersion/FilePath cho cả ba endpoint.
   ============================================================================ */
USE [DiaCompanion];
GO

/* 1. ModelVersions: thêm loại model. */
IF COL_LENGTH('dbo.ModelVersions', 'ModelType') IS NULL
BEGIN
    ALTER TABLE dbo.ModelVersions
        ADD ModelType tinyint NOT NULL
            CONSTRAINT DF_ModelVersions_ModelType DEFAULT (1);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_ModelVersions_ModelType')
BEGIN
    ALTER TABLE dbo.ModelVersions WITH CHECK
        ADD CONSTRAINT CK_ModelVersions_ModelType
        CHECK (ModelType IN (1, 2, 3));
END
GO

/* 2. Bỏ unique index cũ chỉ cho phép đúng 1 IsActive=1 toàn bảng. */
DECLARE @dropSql nvarchar(max) = N'';
SELECT @dropSql = @dropSql +
    N'DROP INDEX ' + QUOTENAME(i.name) + N' ON dbo.ModelVersions;' + CHAR(10)
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID(N'dbo.ModelVersions')
  AND i.is_unique = 1
  AND EXISTS (
      SELECT 1
      FROM sys.index_columns ic
      JOIN sys.columns c
        ON c.object_id = ic.object_id AND c.column_id = ic.column_id
      WHERE ic.object_id = i.object_id
        AND ic.index_id = i.index_id
        AND ic.key_ordinal > 0
        AND c.name = N'IsActive')
  AND 1 = (
      SELECT COUNT(*)
      FROM sys.index_columns ic2
      WHERE ic2.object_id = i.object_id
        AND ic2.index_id = i.index_id
        AND ic2.key_ordinal > 0);

IF LEN(@dropSql) > 0 EXEC sp_executesql @dropSql;
GO

/* 3. Mỗi ModelType chỉ có tối đa một version active. */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ModelVersions')
      AND name = N'UX_ModelVersions_ActivePerType')
BEGIN
    CREATE UNIQUE INDEX UX_ModelVersions_ActivePerType
        ON dbo.ModelVersions(ModelType)
        WHERE IsActive = 1;
END
GO

/* 4. AiDiagnoses lưu thêm version của Lesion và Fractal. */
IF COL_LENGTH('dbo.AiDiagnoses', 'LesionModelVersionId') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD LesionModelVersionId int NULL;
GO

IF COL_LENGTH('dbo.AiDiagnoses', 'FractalModelVersionId') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD FractalModelVersionId int NULL;
GO

/* Dữ liệu lịch sử: backend cũ dùng cùng model version cho cả 3 endpoint. */
UPDATE dbo.AiDiagnoses
SET LesionModelVersionId = ModelVersionId
WHERE LesionModelVersionId IS NULL;

UPDATE dbo.AiDiagnoses
SET FractalModelVersionId = ModelVersionId
WHERE FractalModelVersionId IS NULL;
GO

/* 5. FK + index cho hai model mới. */
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_AiDiagnoses_ModelVersions_LesionModelVersionId')
BEGIN
    ALTER TABLE dbo.AiDiagnoses WITH CHECK
        ADD CONSTRAINT FK_AiDiagnoses_ModelVersions_LesionModelVersionId
        FOREIGN KEY (LesionModelVersionId)
        REFERENCES dbo.ModelVersions(Id);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_AiDiagnoses_ModelVersions_FractalModelVersionId')
BEGIN
    ALTER TABLE dbo.AiDiagnoses WITH CHECK
        ADD CONSTRAINT FK_AiDiagnoses_ModelVersions_FractalModelVersionId
        FOREIGN KEY (FractalModelVersionId)
        REFERENCES dbo.ModelVersions(Id);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.AiDiagnoses')
      AND name = N'IX_AiDiagnoses_LesionModelVersionId')
BEGIN
    CREATE INDEX IX_AiDiagnoses_LesionModelVersionId
        ON dbo.AiDiagnoses(LesionModelVersionId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.AiDiagnoses')
      AND name = N'IX_AiDiagnoses_FractalModelVersionId')
BEGIN
    CREATE INDEX IX_AiDiagnoses_FractalModelVersionId
        ON dbo.AiDiagnoses(FractalModelVersionId);
END
GO

/* 6. Kiểm tra nhanh sau migration. */
SELECT
    ModelType,
    COUNT(*) AS ActiveCount
FROM dbo.ModelVersions
WHERE IsActive = 1
GROUP BY ModelType;
GO
