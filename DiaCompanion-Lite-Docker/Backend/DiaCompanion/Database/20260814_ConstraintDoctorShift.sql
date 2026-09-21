BEGIN TRANSACTION;

-- Xóa constraint cũ chỉ cho phép ca 1 và ca 2.
ALTER TABLE dbo.DoctorShifts
DROP CONSTRAINT CK_DoctorShift_Shift;

-- Tạo lại constraint, cho phép ca 1, ca 2 và ca 3.
ALTER TABLE dbo.DoctorShifts
WITH CHECK ADD CONSTRAINT CK_DoctorShift_Shift
CHECK ([Shift] BETWEEN 1 AND 3);

-- Bật và kiểm tra constraint trên toàn bộ dữ liệu.
ALTER TABLE dbo.DoctorShifts
CHECK CONSTRAINT CK_DoctorShift_Shift;

COMMIT TRANSACTION;