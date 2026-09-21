/* DiaCompanion — schema bổ sung để đồng bộ UC-33 đến UC-61.
   Chạy một lần sau 20260801_AddOptimisticConcurrency.sql. */
USE [DiaCompanion];
GO

IF COL_LENGTH('dbo.PrescriptionItems', 'IsActive') IS NULL
BEGIN
    ALTER TABLE dbo.PrescriptionItems
        ADD IsActive bit NOT NULL
            CONSTRAINT DF_PrescriptionItems_IsActive DEFAULT (1);
END
GO

IF COL_LENGTH('dbo.MedicationLogs', 'ReminderSentAt') IS NULL
BEGIN
    ALTER TABLE dbo.MedicationLogs ADD ReminderSentAt datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.SymptomReports', 'ResponsibleDoctorId') IS NULL
BEGIN
    ALTER TABLE dbo.SymptomReports ADD ResponsibleDoctorId int NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_SymptomReports_Users_ResponsibleDoctorId')
BEGIN
    ALTER TABLE dbo.SymptomReports WITH CHECK
        ADD CONSTRAINT FK_SymptomReports_Users_ResponsibleDoctorId
        FOREIGN KEY (ResponsibleDoctorId) REFERENCES dbo.Users(Id);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PrescriptionItems_PrescriptionId_IsActive'
      AND object_id = OBJECT_ID('dbo.PrescriptionItems'))
BEGIN
    CREATE INDEX IX_PrescriptionItems_PrescriptionId_IsActive
        ON dbo.PrescriptionItems(PrescriptionId, IsActive);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_MedicationLogs_Status_ScheduledAt_ReminderSentAt'
      AND object_id = OBJECT_ID('dbo.MedicationLogs'))
BEGIN
    CREATE INDEX IX_MedicationLogs_Status_ScheduledAt_ReminderSentAt
        ON dbo.MedicationLogs(Status, ScheduledAt, ReminderSentAt);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_SymptomReports_ResponsibleDoctorId_CreatedAt'
      AND object_id = OBJECT_ID('dbo.SymptomReports'))
BEGIN
    CREATE INDEX IX_SymptomReports_ResponsibleDoctorId_CreatedAt
        ON dbo.SymptomReports(ResponsibleDoctorId, CreatedAt);
END
GO


/* Không tự ý xoá dữ liệu cũ. Nếu có feedback trỏ tới Visit không tồn tại,
   dừng nâng cấp để quản trị viên đối soát trước khi thêm khóa ngoại. */
IF EXISTS (
    SELECT 1
    FROM dbo.Feedbacks f
    LEFT JOIN dbo.Visits v ON v.Id = f.VisitId
    WHERE f.VisitId IS NOT NULL AND v.Id IS NULL)
BEGIN
    THROW 51001, N'Có Feedback.VisitId không tồn tại trong Visits. Hãy làm sạch dữ liệu trước khi chạy lại script.', 1;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_Feedbacks_Visits_VisitId')
BEGIN
    ALTER TABLE dbo.Feedbacks WITH CHECK
        ADD CONSTRAINT FK_Feedbacks_Visits_VisitId
        FOREIGN KEY (VisitId) REFERENCES dbo.Visits(Id);
END
GO

/* UC-51: mỗi bệnh nhân chỉ phản hồi một lần cho một lượt khám. Nếu dữ liệu
   cũ đang trùng, dừng lại thay vì âm thầm chọn/xoá một bản ghi. */
IF EXISTS (
    SELECT 1
    FROM dbo.Feedbacks
    WHERE VisitId IS NOT NULL AND IsDeleted = 0
    GROUP BY PatientId, VisitId
    HAVING COUNT_BIG(*) > 1)
BEGIN
    THROW 51002, N'Tồn tại nhiều feedback đang hoạt động cho cùng PatientId/VisitId. Hãy xử lý bản ghi trùng trước khi chạy lại script.', 1;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Feedbacks_PatientId_VisitId'
      AND object_id = OBJECT_ID('dbo.Feedbacks'))
BEGIN
    CREATE UNIQUE INDEX IX_Feedbacks_PatientId_VisitId
        ON dbo.Feedbacks(PatientId, VisitId)
        WHERE VisitId IS NOT NULL AND IsDeleted = 0;
END
GO

/* Gán bác sĩ phụ trách cho dữ liệu triệu chứng cũ theo lượt khám gần nhất. */
UPDATE sr
SET ResponsibleDoctorId = recent.DoctorId
FROM dbo.SymptomReports sr
OUTER APPLY
(
    SELECT TOP (1) v.DoctorId
    FROM dbo.Visits v
    WHERE v.PatientId = sr.PatientId
      AND v.DoctorId IS NOT NULL
    ORDER BY v.VisitDate DESC, v.Id DESC
) recent
WHERE sr.ResponsibleDoctorId IS NULL
  AND recent.DoctorId IS NOT NULL;
GO


/* fix delete role 2 */
UPDATE dbo.Users
SET Role = 1,
    UpdatedAt = SYSUTCDATETIME()
WHERE Role = 2;



ALTER TABLE dbo.Users
DROP CONSTRAINT CK_Users_Role;
GO

ALTER TABLE dbo.Users
ADD CONSTRAINT CK_Users_Role
CHECK (Role IN (0, 1, 3, 4));
GO