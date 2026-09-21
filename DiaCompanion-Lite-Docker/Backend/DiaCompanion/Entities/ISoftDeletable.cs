namespace DiaCompanion.Api.Entities;

/// <summary>
/// QT-5: dữ liệu do bệnh nhân tự nhập dùng xoá mềm. KHÔNG dùng chung với
/// IVoidable trên cùng một bảng — hai cơ chế song song sẽ có chỗ quên lọc.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
