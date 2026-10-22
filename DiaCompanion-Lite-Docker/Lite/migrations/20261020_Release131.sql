/* DR AI-Diagnosis 1.3.1
   1) Gói chấm mù cho máy khác (GradingPackages, GradingPackageItems, BlindGrades.SourcePackageId).
   2) Mỗi mắt của một lượt khám chỉ một ảnh còn hiệu lực (chỉ mục duy nhất có lọc).
   3) Mỗi ảnh chỉ một kết quả AI còn hiệu lực (chỉ mục duy nhất có lọc).
   Idempotent: chạy lại nhiều lần không lỗi. Bộ cài/dbinit áp mọi tệp trong migrations mỗi lần cài/cài đè.
   Cài mới: schema sinh từ EF đã có bảng gói chấm, các khối tạo bảng tự bỏ qua. */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* ------------------------------------------------------------ 1. gói chấm */
IF OBJECT_ID(N'dbo.GradingPackages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GradingPackages (
        Id         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_GradingPackages PRIMARY KEY,
        Code       nvarchar(40)  NOT NULL,
        [Round]    tinyint       NOT NULL,
        GraderId   int           NOT NULL,
        [Key]      nvarchar(64)  NOT NULL,
        Status     tinyint       NOT NULL,
        Note       nvarchar(500) NULL,
        CreatedBy  int           NOT NULL,
        CreatedAt  datetime2     NOT NULL,
        ClosedAt   datetime2     NULL,
        CONSTRAINT FK_GradingPackages_Users_GraderId FOREIGN KEY (GraderId) REFERENCES dbo.Users (Id),
        CONSTRAINT CK_GradingPackages_Round CHECK ([Round] IN (2, 3))
    );
    CREATE UNIQUE INDEX UX_GradingPackages_Code ON dbo.GradingPackages (Code);
    CREATE INDEX IX_GradingPackages_RoundStatus ON dbo.GradingPackages ([Round], Status);
    CREATE INDEX IX_GradingPackages_GraderId ON dbo.GradingPackages (GraderId);
END;

IF OBJECT_ID(N'dbo.GradingPackageItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GradingPackageItems (
        Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_GradingPackageItems PRIMARY KEY,
        PackageId     int          NOT NULL,
        FundusImageId int          NOT NULL,
        Token         nvarchar(16) NOT NULL,
        Position      int          NOT NULL,
        BlindGradeId  int          NULL,
        ImportedAt    datetime2    NULL,
        CONSTRAINT FK_GradingPackageItems_GradingPackages_PackageId FOREIGN KEY (PackageId)
            REFERENCES dbo.GradingPackages (Id) ON DELETE CASCADE,
        CONSTRAINT FK_GradingPackageItems_FundusImages_FundusImageId FOREIGN KEY (FundusImageId)
            REFERENCES dbo.FundusImages (Id),
        CONSTRAINT FK_GradingPackageItems_BlindGrades_BlindGradeId FOREIGN KEY (BlindGradeId)
            REFERENCES dbo.BlindGrades (Id)
    );
    CREATE UNIQUE INDEX UX_GradingPackageItems_Token ON dbo.GradingPackageItems (PackageId, Token);
    CREATE UNIQUE INDEX UX_GradingPackageItems_Image ON dbo.GradingPackageItems (PackageId, FundusImageId);
    CREATE INDEX IX_GradingPackageItems_Image ON dbo.GradingPackageItems (FundusImageId);
    CREATE INDEX IX_GradingPackageItems_BlindGradeId ON dbo.GradingPackageItems (BlindGradeId);
END;

IF COL_LENGTH(N'dbo.BlindGrades', N'SourcePackageId') IS NULL
BEGIN
    ALTER TABLE dbo.BlindGrades ADD SourcePackageId int NULL;
END;
GO

IF OBJECT_ID(N'dbo.FK_BlindGrades_GradingPackages_SourcePackageId', N'F') IS NULL
    ALTER TABLE dbo.BlindGrades WITH CHECK ADD CONSTRAINT FK_BlindGrades_GradingPackages_SourcePackageId
        FOREIGN KEY (SourcePackageId) REFERENCES dbo.GradingPackages (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BlindGrades_SourcePackageId' AND object_id = OBJECT_ID(N'dbo.BlindGrades'))
    CREATE INDEX IX_BlindGrades_SourcePackageId ON dbo.BlindGrades (SourcePackageId);
GO

/* ------------------------------------------------------------ 2. mỗi mắt một ảnh */
/* Lượt khám đang có ảnh nạp trùng (lỗi bấm hai lần ở bản ≤ 1.3.0) thì CHƯA tạo chỉ mục: không tự thu hồi ảnh nào.
   Mở lượt đó trên giao diện, mục "Ảnh nạp trùng", thu hồi ảnh thừa; lần cài/cài đè sau chỉ mục được tạo.
   Phần mềm vẫn chặn nạp trùng ở tầng ứng dụng kể cả khi chưa có chỉ mục. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_FundusImages_VisitEye_Active' AND object_id = OBJECT_ID(N'dbo.FundusImages'))
BEGIN
    IF EXISTS (
        SELECT 1 FROM dbo.FundusImages
        WHERE IsVoided = 0 AND VisitId IS NOT NULL
        GROUP BY VisitId, Eye HAVING COUNT(*) > 1)
    BEGIN
        DECLARE @dups nvarchar(max) = (
            SELECT STRING_AGG(CONCAT(N'lượt ', VisitId, N' mắt ', CASE Eye WHEN 0 THEN N'OD' ELSE N'OS' END,
                                     N' (ảnh ', ids, N')'), N'; ')
            FROM (SELECT VisitId, Eye, STRING_AGG(CAST(Id AS nvarchar(20)), N',') AS ids
                  FROM dbo.FundusImages
                  WHERE IsVoided = 0 AND VisitId IS NOT NULL
                  GROUP BY VisitId, Eye HAVING COUNT(*) > 1) d);
        PRINT N'CẢNH BÁO 1.3.1: còn ảnh nạp trùng cùng mắt, chưa tạo chỉ mục UX_FundusImages_VisitEye_Active: ' + @dups;
    END
    ELSE
        CREATE UNIQUE INDEX UX_FundusImages_VisitEye_Active ON dbo.FundusImages (VisitId, Eye)
            WHERE IsVoided = 0 AND VisitId IS NOT NULL;
END;
GO

/* ------------------------------------------------------------ 3. mỗi ảnh một kết quả AI */
/* Lỗi ≤ 1.3.1 bản đầu: hai mắt chạy AI xong cùng lúc gây deadlock, lần thử lại tự động ghi THÊM kết quả của
   lần hỏng, nên một ảnh có hai kết quả AI còn hiệu lực ("Còn 1/3 kết quả AI chưa được bác sĩ phê duyệt").
   Còn bản trùng thì CHƯA tạo chỉ mục, không tự thu hồi: mở lượt (mục "Kết quả trùng") để thu hồi bản cũ. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AiDiagnoses_Image_Active' AND object_id = OBJECT_ID(N'dbo.AiDiagnoses'))
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.AiDiagnoses WHERE IsVoided = 0 GROUP BY FundusImageId HAVING COUNT(*) > 1)
    BEGIN
        DECLARE @dx nvarchar(max) = (
            SELECT STRING_AGG(CONCAT(N'ảnh ', FundusImageId, N' (kết quả ', ids, N')'), N'; ')
            FROM (SELECT FundusImageId, STRING_AGG(CAST(Id AS nvarchar(20)), N',') AS ids
                  FROM dbo.AiDiagnoses WHERE IsVoided = 0
                  GROUP BY FundusImageId HAVING COUNT(*) > 1) d);
        PRINT N'CẢNH BÁO 1.3.1: còn ảnh có hai kết quả AI còn hiệu lực, chưa tạo chỉ mục UX_AiDiagnoses_Image_Active: ' + @dx;
    END
    ELSE
        CREATE UNIQUE INDEX UX_AiDiagnoses_Image_Active ON dbo.AiDiagnoses (FundusImageId) WHERE IsVoided = 0;
END;
GO
