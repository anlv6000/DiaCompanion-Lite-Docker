// ============================================================================
//  DTO cho nghiệp vụ LỄ TÂN — thêm vào src/DiaCompanion.Api/Dtos/Dtos.cs
//  (hoặc để riêng file này trong thư mục Dtos, cùng namespace)
// ============================================================================
using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/* =============================== CA TRỰC ================================= */

/// <summary>Một dòng ca trực cố định để hiển thị / quản lý.</summary>
public class DoctorShiftDto
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = "";
    public string? LicenseNo { get; set; }

    /// <summary>0 = Chủ nhật … 6 = Thứ 7 (theo System.DayOfWeek).</summary>
    public byte DayOfWeek { get; set; }
    /// <summary>Nhãn tiếng Việt sẵn cho client: "Thứ 2", "Chủ nhật"…</summary>
    public string DayLabel { get; set; } = "";

    public byte Shift { get; set; }          // 1 = Sáng, 2 = Chiều, 3 = Đêm
    public string ShiftLabel { get; set; } = "";
    public bool IsActive { get; set; }

    public string RowVersion { get; set; } = "";
}

/// <summary>Tạo một dòng ca trực.</summary>
public class CreateDoctorShiftRequest
{
    [Required] public int DoctorId { get; set; }

    /// <summary>0..6 theo System.DayOfWeek (0 = Chủ nhật).</summary>
    [Range(0, 6)] public byte DayOfWeek { get; set; }

    /// <summary>Ca Sáng, Chiều hoặc Tối.</summary>
    [Range(1, 3)] public byte Shift { get; set; }
}

/// <summary>Tạo nhiều ca cùng lúc cho một bác sĩ (VD: Sáng T2, T4, T6).</summary>
public class CreateDoctorShiftsBatchRequest
{
    [Required] public int DoctorId { get; set; }
    /// <summary>Danh sách thứ trong tuần cần thêm (0..6).</summary>
    [Required, MinLength(1)] public List<byte> DaysOfWeek { get; set; } = new();
    [Range(1, 3)] public byte Shift { get; set; }
}

/* ===================== BÁC SĨ ĐANG TRỰC (để gán) ======================== */

/// <summary>
/// Bác sĩ đang trực trong một ca của một ngày — dữ liệu lễ tân chọn khi gán
/// lượt khám. Kèm số lượt khám đang mở để lễ tân biết ai đang rảnh hơn.
/// </summary>
public class OnDutyDoctorDto
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = "";
    public string? LicenseNo { get; set; }
    public byte Shift { get; set; }
    public string ShiftLabel { get; set; } = "";
    /// <summary>Số lượt khám đang mở (InProgress) hôm nay của bác sĩ này.</summary>
    public int OpenVisitCount { get; set; }
}

/// <summary>Kết quả tra cứu bác sĩ trực theo ngày.</summary>
public class OnDutyResponse
{
    public DateOnly Date { get; set; }
    public string DayLabel { get; set; } = "";
    /// <summary>Ca hiện tại suy theo giờ máy chủ: 1 = Sáng, 2 = Chiều, 3 = Tối (tham khảo).</summary>
    public byte? CurrentShift { get; set; }
    public List<OnDutyDoctorDto> Doctors { get; set; } = new();
}
