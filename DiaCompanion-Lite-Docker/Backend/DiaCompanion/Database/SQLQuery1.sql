USE [DiaCompanion];
GO

/* ============================================================
   Add VisitId to HealthMetrics
   HealthMetric có thể:
   - thuộc một lượt khám: VisitId != NULL
   - bệnh nhân tự nhập ngoài lượt khám: VisitId = NULL
   ============================================================ */

IF COL_LENGTH('dbo.HealthMetrics', 'VisitId') IS NULL
BEGIN
    ALTER TABLE dbo.HealthMetrics
        ADD VisitId INT NULL;
END
GO


/* ============================================================
   Foreign Key:
   HealthMetrics.VisitId -> MedicalVisits.Id
   ============================================================ */

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_HealthMetrics_MedicalVisits_VisitId'
)
BEGIN
    ALTER TABLE dbo.HealthMetrics WITH CHECK
        ADD CONSTRAINT FK_HealthMetrics_MedicalVisits_VisitId
        FOREIGN KEY (VisitId)
        REFERENCES dbo.MedicalVisits(Id)
        ON DELETE NO ACTION;
END
GO


/* ============================================================
   Index để query metric theo Visit nhanh hơn
   ============================================================ */

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_HealthMetrics_VisitId'
      AND object_id = OBJECT_ID('dbo.HealthMetrics')
)
BEGIN
    CREATE INDEX IX_HealthMetrics_VisitId
        ON dbo.HealthMetrics(VisitId)
        WHERE VisitId IS NOT NULL;
END
GO


/* Index cũ không còn cần thiết vì index mới
   bắt đầu bằng VisitId và phục vụ luôn query theo VisitId */
IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_HealthMetrics_VisitId'
      AND object_id = OBJECT_ID('dbo.HealthMetrics')
)
BEGIN
    DROP INDEX IX_HealthMetrics_VisitId
    ON dbo.HealthMetrics;
END
GO


/* Một Visit chỉ có 1 metric cùng loại đang active */
IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_HealthMetrics_VisitId_MetricType'
      AND object_id = OBJECT_ID('dbo.HealthMetrics')
)
BEGIN
    CREATE UNIQUE INDEX UX_HealthMetrics_VisitId_MetricType
    ON dbo.HealthMetrics
    (
        VisitId,
        MetricType
    )
    WHERE VisitId IS NOT NULL
      AND IsDeleted = 0;
END
GO