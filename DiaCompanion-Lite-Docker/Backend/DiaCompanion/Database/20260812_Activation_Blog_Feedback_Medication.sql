USE [DiaCompanion];
GO

/* ============================================================
   MedicationLogs.Status
   NEW:
   0 = Pending
   1 = Taken
   2 = Missed
   3 = Skipped
   4 = Cancelled
   ============================================================ */

-- 1. Xóa constraint cũ trước
IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_MedLog_Status'
      AND parent_object_id = OBJECT_ID(N'dbo.MedicationLogs')
)
BEGIN
    ALTER TABLE dbo.MedicationLogs
        DROP CONSTRAINT CK_MedLog_Status;
END
GO


-- 2. Chỉ migrate nếu DB chưa migrate trước đó
IF OBJECT_ID(N'dbo.MedicationLogs', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.extended_properties ep
       JOIN sys.columns c
         ON c.object_id = ep.major_id
        AND c.column_id = ep.minor_id
       WHERE ep.major_id = OBJECT_ID(N'dbo.MedicationLogs')
         AND c.name = N'Status'
         AND ep.name = N'DiaCompanion_MedicationStatus_SkippedIs3'
   )
BEGIN
    -- Giá trị tạm để swap 3 <-> 4
    UPDATE dbo.MedicationLogs
    SET Status = 255
    WHERE Status = 3;

    UPDATE dbo.MedicationLogs
    SET Status = 3
    WHERE Status = 4;

    UPDATE dbo.MedicationLogs
    SET Status = 4
    WHERE Status = 255;

    EXEC sys.sp_addextendedproperty
        @name = N'DiaCompanion_MedicationStatus_SkippedIs3',
        @value = N'0=Pending;1=Taken;2=Missed;3=Skipped;4=Cancelled',
        @level0type = N'SCHEMA',
        @level0name = N'dbo',
        @level1type = N'TABLE',
        @level1name = N'MedicationLogs',
        @level2type = N'COLUMN',
        @level2name = N'Status';
END
GO


-- 3. Tạo constraint mới 0 -> 4
ALTER TABLE dbo.MedicationLogs WITH CHECK
ADD CONSTRAINT CK_MedLog_Status
CHECK ([Status] >= 0 AND [Status] <= 4);
GO

ALTER TABLE dbo.MedicationLogs
CHECK CONSTRAINT CK_MedLog_Status;
GO