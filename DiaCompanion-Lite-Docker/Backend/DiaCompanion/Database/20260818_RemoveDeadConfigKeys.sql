/* ===========================================================================
   Dọn các khoá cấu hình đã chết khỏi dbo.SystemConfigs.

   Lý do: sau khi độ tin cậy nghỉ hưu khỏi quyết định (Gap 2) và ngưỡng đường
   huyết chuyển hẳn sang hằng số lâm sàng cố định trong GlucoseThresholds.cs
   (phân biệt theo type và ngữ cảnh bữa ăn, neo ADA), bốn khoá dưới đây không
   còn được bất kỳ dòng mã nào đọc. Giữ lại chỉ gây hiểu nhầm là 'cấu hình
   được' và dễ bị chất vấn khi bảo vệ.

     - ai.confidence_threshold      : tín hiệu độ tin cậy đã khai tử
     - metric.glucose_fasting_max   : thay bằng GlucoseThresholds (cố định)
     - metric.glucose_postmeal_max  : nt
     - metric.glucose_min           : nt

   KHÔNG đụng dữ liệu chẩn đoán: cột Confidence trên dbo.AiDiagnoses vẫn giữ
   nguyên để giải thích các lần chạy cũ. Đây chỉ xoá khoá cấu hình, không xoá
   bản ghi lâm sàng.

   Script idempotent — chạy lại nhiều lần không sao (DELETE theo danh sách khoá,
   không có thì bỏ qua).
   =========================================================================== */
SET NOCOUNT ON;
GO

DELETE FROM dbo.SystemConfigs
WHERE [Key] IN (
    'ai.confidence_threshold',
    'metric.glucose_fasting_max',
    'metric.glucose_postmeal_max',
    'metric.glucose_min'
);
GO
