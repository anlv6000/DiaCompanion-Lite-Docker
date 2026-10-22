using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

/// <summary>
/// 1.3.1: gói chấm mù đưa sang máy khác (chấm offline bằng trình duyệt).
/// Máy 1 (bác sĩ khám, chấm vòng 1, chạy AI) tạo gói vòng 2 hoặc 3 cho MỘT người chấm, xuất ra tệp zip
/// chỉ gồm ảnh đã đổi tên ngẫu nhiên (bỏ metadata) và trang chấm. Người chấm trả về tệp kết quả JSON,
/// máy 1 nhập vào thành BlindGrade. Bảng ánh xạ mã ngẫu nhiên → ảnh chỉ nằm trên máy 1.
/// </summary>
public class GradingPackage
{
    public int Id { get; set; }

    /// <summary>Mã gói hiện cho người dùng, vd V2-20261020-K7QM. Duy nhất.</summary>
    [Required, MaxLength(40)] public string Code { get; set; } = "";

    /// <summary>Vòng chấm: 2 (người chấm độc lập) hoặc 3 (phân xử).</summary>
    public byte Round { get; set; }

    /// <summary>Tài khoản (vai trò Grader) đại diện người chấm ở máy kia. Phân độ nhập về ghi theo tài khoản này.</summary>
    public int GraderId { get; set; }
    public User? Grader { get; set; }

    /// <summary>Khóa ngẫu nhiên nhúng trong gói; tệp kết quả phải mang đúng khóa mới nhập được
    /// (chống nhập nhầm tệp của gói khác, không phải cơ chế mật mã chống người chấm).</summary>
    [Required, MaxLength(64)] public string Key { get; set; } = "";

    public GradingPackageStatus Status { get; set; } = GradingPackageStatus.Open;
    [MaxLength(500)] public string? Note { get; set; }

    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Thời điểm chuyển sang Completed hoặc Cancelled.</summary>
    public DateTime? ClosedAt { get; set; }

    public ICollection<GradingPackageItem> Items { get; set; } = new List<GradingPackageItem>();
}

/// <summary>Một ảnh trong gói. Token là tên ảnh trong gói (ngẫu nhiên, không suy ra được Id ảnh).</summary>
public class GradingPackageItem
{
    public int Id { get; set; }
    public int PackageId { get; set; }
    public GradingPackage? Package { get; set; }

    public int FundusImageId { get; set; }
    public FundusImage? FundusImage { get; set; }

    [Required, MaxLength(16)] public string Token { get; set; } = "";
    /// <summary>Thứ tự hiển thị trong gói (đã xáo trộn), bắt đầu từ 1.</summary>
    public int Position { get; set; }

    /// <summary>Phân độ đã nhập về từ gói (null = chưa nhập).</summary>
    public int? BlindGradeId { get; set; }
    public BlindGrade? BlindGrade { get; set; }
    public DateTime? ImportedAt { get; set; }
}
