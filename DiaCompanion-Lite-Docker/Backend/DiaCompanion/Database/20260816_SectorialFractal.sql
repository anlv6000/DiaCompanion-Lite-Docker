/* ===========================================================================
   20260816_SectorialFractal.sql

   Gap 3 — Sectorial Fractal Analysis.
   Thêm 7 cột số vào AiDiagnoses để lưu chỉ số fractal theo vùng.

   BỐI CẢNH: trước đây FD_thick / FD_thin / delta_FD chỉ được nhét vào chuỗi
   văn bản FractalNote (nvarchar 300), nên không vẽ biểu đồ, không so sánh giữa
   các lần khám và không chạy thống kê được. Bảy cột dưới đây đưa toàn bộ chỉ
   số về dạng số.

   AN TOÀN: chỉ THÊM cột nullable, không xoá và không sửa cột nào. Bản ghi cũ
   giữ nguyên, bảy cột mới để NULL. Script idempotent, chạy lại nhiều lần không
   sao.

   Chạy TRƯỚC khi triển khai code mới.
   =========================================================================== */

SET NOCOUNT ON;
GO

/* --- FD từng góc phần tư -------------------------------------------------
   Đã chuẩn hoá theo mắt: ảnh mắt trái (OS) được lật ngang trước khi chia, nên
   với mọi bản ghi, ST/IT luôn là phía thái dương và SN/IN luôn là phía mũi.
   Vùng đĩa thị đã bị loại trước khi tính.

   KHÔNG so trực tiếp với FractalDimension: dải hộp và diện tích khác nhau.
   decimal(6,4) đủ cho khoảng giá trị 0.0000–99.9999; thực tế FD nằm quanh 1.0–1.5.
--------------------------------------------------------------------------- */
IF COL_LENGTH('dbo.AiDiagnoses', 'FractalSt') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD FractalSt decimal(6,4) NULL;
GO
IF COL_LENGTH('dbo.AiDiagnoses', 'FractalSn') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD FractalSn decimal(6,4) NULL;
GO
IF COL_LENGTH('dbo.AiDiagnoses', 'FractalIt') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD FractalIt decimal(6,4) NULL;
GO
IF COL_LENGTH('dbo.AiDiagnoses', 'FractalIn') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD FractalIn decimal(6,4) NULL;
GO

/* --- Chỉ dấu chính: bất đối xứng giữa các vùng ---------------------------
   Độ lệch chuẩn của bốn giá trị trên. Là tỉ số nội ảnh nên tự triệt tiêu phần
   lớn yếu tố tác động đồng đều lên cả bốn vùng (độ sáng, độ phân giải, đặc
   tính máy chụp) — ổn định hơn FD tuyệt đối khi so theo thời gian.
--------------------------------------------------------------------------- */
IF COL_LENGTH('dbo.AiDiagnoses', 'FractalAsymmetry') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD FractalAsymmetry decimal(6,4) NULL;
GO

/* --- Chênh lệch temporal trừ nasal, CÓ DẤU -------------------------------
   (ST + IT)/2 − (SN + IN)/2. Dương = phía thái dương phức tạp hơn.
   Cần 7 chữ số vì mang dấu âm.
--------------------------------------------------------------------------- */
IF COL_LENGTH('dbo.AiDiagnoses', 'FractalTn') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD FractalTn decimal(7,4) NULL;
GO

/* --- Lacunarity ----------------------------------------------------------
   Hộp trượt trên toàn ảnh. Khoảng giá trị rộng hơn FD nên dùng decimal(8,4).
--------------------------------------------------------------------------- */
IF COL_LENGTH('dbo.AiDiagnoses', 'Lacunarity') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD Lacunarity decimal(8,4) NULL;
GO

/* --- Chỉ mục cho truy vấn theo dõi dọc -----------------------------------
   ProgressionPage sẽ lọc theo ảnh và sắp theo thời gian. Lọc bản ghi đã thu
   hồi ngay trong chỉ mục để không phải quét thừa.
--------------------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_AiDiagnoses_Fractal_Progression'
      AND object_id = OBJECT_ID('dbo.AiDiagnoses'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AiDiagnoses_Fractal_Progression
        ON dbo.AiDiagnoses (FundusImageId, CreatedAt)
        INCLUDE (FractalDimension, FractalAsymmetry, FractalTn)
        WHERE IsVoided = 0;
END
GO

/* --- Kiểm tra sau khi chạy ----------------------------------------------- */
SELECT
    c.name        AS ColumnName,
    t.name        AS DataType,
    c.precision   AS [Precision],
    c.scale       AS Scale,
    c.is_nullable AS IsNullable
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.AiDiagnoses')
  AND c.name IN ('FractalDimension','FractalSt','FractalSn','FractalIt',
                 'FractalIn','FractalAsymmetry','FractalTn','Lacunarity')
ORDER BY c.column_id;
GO

/* Kỳ vọng: 8 dòng. Nếu thiếu dòng nào thì lệnh ALTER tương ứng chưa chạy. */
