/* ============================================================================
   DiaCompanion — optimistic concurrency patch
   SQL Server 2019+

   Chạy một lần trên database hiện tại trước khi khởi động backend đã chỉnh.
   Script idempotent: cột đã tồn tại sẽ được bỏ qua.
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* --------------------------- rowversion columns --------------------------- */
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Users', N'RowVer') IS NULL
    ALTER TABLE dbo.Users ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.Patients', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Patients', N'RowVer') IS NULL
    ALTER TABLE dbo.Patients ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.Visits', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Visits', N'RowVer') IS NULL
    ALTER TABLE dbo.Visits ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.FundusImages', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.FundusImages', N'RowVer') IS NULL
    ALTER TABLE dbo.FundusImages ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.AiDiagnoses', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.AiDiagnoses', N'RowVer') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.DiagnosisReviews', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.DiagnosisReviews', N'RowVer') IS NULL
    ALTER TABLE dbo.DiagnosisReviews ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.Prescriptions', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Prescriptions', N'RowVer') IS NULL
    ALTER TABLE dbo.Prescriptions ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.MedicationLogs', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.MedicationLogs', N'RowVer') IS NULL
    ALTER TABLE dbo.MedicationLogs ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.SystemConfigs', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.SystemConfigs', N'RowVer') IS NULL
    ALTER TABLE dbo.SystemConfigs ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.ModelVersions', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ModelVersions', N'RowVer') IS NULL
    ALTER TABLE dbo.ModelVersions ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.BlogPosts', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.BlogPosts', N'RowVer') IS NULL
    ALTER TABLE dbo.BlogPosts ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.HealthMetrics', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.HealthMetrics', N'RowVer') IS NULL
    ALTER TABLE dbo.HealthMetrics ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.LifestyleLogs', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.LifestyleLogs', N'RowVer') IS NULL
    ALTER TABLE dbo.LifestyleLogs ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.SymptomReports', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.SymptomReports', N'RowVer') IS NULL
    ALTER TABLE dbo.SymptomReports ADD RowVer rowversion;

IF OBJECT_ID(N'dbo.DoctorShifts', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.DoctorShifts', N'RowVer') IS NULL
    ALTER TABLE dbo.DoctorShifts ADD RowVer rowversion;

/* -------------------------- supporting audit fields ----------------------- */
IF OBJECT_ID(N'dbo.Prescriptions', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Prescriptions', N'UpdatedAt') IS NULL
    ALTER TABLE dbo.Prescriptions ADD UpdatedAt datetime2 NULL;

/*
   Triage phải UPDATE chính AiDiagnoses khi tạo review; nếu chỉ INSERT review,
   RowVer của AiDiagnoses không tham gia điều kiện optimistic concurrency.
*/
IF OBJECT_ID(N'dbo.AiDiagnoses', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.AiDiagnoses', N'LastReviewActionBy') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD LastReviewActionBy int NULL;

IF OBJECT_ID(N'dbo.AiDiagnoses', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.AiDiagnoses', N'LastReviewActionAt') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD LastReviewActionAt datetime2 NULL;

COMMIT TRANSACTION;

PRINT N'Optimistic concurrency columns are ready.';
