using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

/// <summary>
/// Ca trực CỐ ĐỊNH theo tuần của bác sĩ (LT-1).
///
/// Mỗi dòng nói: "bác sĩ X trực ca Y vào thứ Z hằng tuần". Ví dụ BS An trực
/// ca Sáng vào Thứ 2, Thứ 4, Thứ 6 sẽ là 3 dòng. Lễ tân dựa vào bảng này để
/// biết hôm nay ai đang trực mà gán vào lượt khám.
///
/// Không lưu theo từng ngày cụ thể — đây là mẫu lặp hằng tuần. Muốn nghỉ đột
/// xuất một hôm thì tắt IsActive tạm hoặc xử lý ở tầng nghiệp vụ; phiên bản
/// này giữ đơn giản đúng phạm vi capstone.
/// </summary>
public class DoctorShift : IHasRowVersion
{
    public int Id { get; set; }

    /// <summary>Bác sĩ trực. Quyền Doctor được xác định qua Roles/UserRoles đang active.</summary>
    public int DoctorId { get; set; }
    public User? Doctor { get; set; }

    /// <summary>
    /// Thứ trong tuần theo chuẩn .NET System.DayOfWeek: 0 = Chủ nhật, 1 = Thứ 2,
    /// … 6 = Thứ 7. Dùng thẳng giá trị enum của .NET để so khớp
    /// DateTime.DayOfWeek không phải ánh xạ lại.
    /// </summary>
    public byte DayOfWeek { get; set; }

    /// <summary>Ca Sáng, Chiều hoặc Đêm </summary>
    public ShiftType Shift { get; set; }

    /// <summary>Tắt tạm khi bác sĩ nghỉ dài mà không muốn xoá lịch.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Lễ tân / quản trị đã tạo dòng lịch này.</summary>
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }


    public byte[] RowVer { get; set; } = Array.Empty<byte>();
}
