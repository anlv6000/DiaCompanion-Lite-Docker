/* DiaCompanion Lite 1.2 — chấm mù cho nghiên cứu.
   Idempotent: chạy lại nhiều lần không lỗi. Bộ cài áp mọi tệp trong db\migrations mỗi lần cài/cài đè.
   Cài mới: schema sinh từ EF đã có bảng BlindGrades, khối tạo bảng sẽ bỏ qua. */
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.BlindGrades', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BlindGrades (
        Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_BlindGrades PRIMARY KEY,
        FundusImageId int           NOT NULL,
        [Round]       tinyint       NOT NULL,
        GraderId      int           NOT NULL,
        Gradable      bit           NOT NULL,
        Grade         tinyint       NULL,
        Note          nvarchar(500) NULL,
        CreatedAt     datetime2     NOT NULL,
        CONSTRAINT FK_BlindGrades_FundusImages_FundusImageId FOREIGN KEY (FundusImageId) REFERENCES dbo.FundusImages (Id),
        CONSTRAINT FK_BlindGrades_Users_GraderId FOREIGN KEY (GraderId) REFERENCES dbo.Users (Id),
        CONSTRAINT CK_BlindGrades_Round CHECK ([Round] BETWEEN 1 AND 3)
    );
    CREATE UNIQUE INDEX UX_BlindGrades_ImageRound ON dbo.BlindGrades (FundusImageId, [Round]);
    CREATE INDEX IX_BlindGrades_GraderId ON dbo.BlindGrades (GraderId);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Grader')
    INSERT INTO dbo.Roles (Id, Name, DisplayName, Description, IsActive, CreatedAt)
    VALUES (5, 'Grader', N'Người chấm độc lập', N'Chấm mù ảnh đáy mắt vòng 2/3; không thấy AI, không truy cập hồ sơ', 1, SYSUTCDATETIME());
