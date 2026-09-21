/* ===========================================================================
   Điểm nguy cơ nền và ngưỡng bất đồng hiệu dụng (Gap 2, thiết kế mới).

   KHÔNG xoá cột Confidence: dữ liệu lịch sử của mọi lần chạy trước được giữ
   nguyên, và các bản ghi cũ vẫn giải thích được vì sao từng bị chuyển bác sĩ.
   Cột chỉ ngừng được ghi giá trị mới và ngừng hiển thị.

   Script idempotent — chạy lại nhiều lần không sao.
   =========================================================================== */
SET NOCOUNT ON;
GO

/* Điểm nguy cơ nền tại thời điểm chạy. Lưu vào bản ghi chứ không tính lại khi
   xem, vì HbA1c và tuân thủ thuốc thay đổi theo thời gian — tính lại sẽ cho ra
   con số khác con số đã dùng để ra quyết định. */
IF COL_LENGTH('dbo.AiDiagnoses', 'ClinicalRiskScore') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD ClinicalRiskScore tinyint NULL;
GO

/* Ngưỡng bất đồng THẬT SỰ đã áp dụng sau khi hạ theo nguy cơ nền.
   Cần lưu để về sau giải thích được vì sao hai ca cùng disagreement = 0.25 mà
   một ca bị chuyển còn ca kia thì không. */
IF COL_LENGTH('dbo.AiDiagnoses', 'EffectiveDisagreementThreshold') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD EffectiveDisagreementThreshold decimal(4,3) NULL;
GO

/* Các yếu tố đã cộng điểm, dạng văn bản, để giao diện giải thích được thay vì
   chỉ hiện một con số trần. */
IF COL_LENGTH('dbo.AiDiagnoses', 'ClinicalRiskFactors') IS NULL
    ALTER TABLE dbo.AiDiagnoses ADD ClinicalRiskFactors nvarchar(500) NULL;
GO

/* Hàng đợi phân loại sắp theo IsDeferred -> Disagreement -> ClinicalRiskScore. */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_AiDiagnoses_TriagePriority'
      AND object_id = OBJECT_ID('dbo.AiDiagnoses'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AiDiagnoses_TriagePriority
        ON dbo.AiDiagnoses (IsDeferred DESC, Disagreement DESC, ClinicalRiskScore DESC)
        WHERE IsVoided = 0;
END
GO
