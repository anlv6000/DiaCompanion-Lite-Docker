using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

/// <summary>
/// Phân độ CHẤM MÙ của một ảnh đáy mắt, dùng làm nhãn tham chiếu nghiên cứu.
/// Round 1 = bác sĩ khám chấm trước khi chạy AI; Round 2 = người chấm độc lập;
/// Round 3 = người phân xử (chỉ các ảnh round 1 và 2 bất đồng).
/// Bất biến: không sửa, không xóa sau khi lưu (tính mù phải kiểm chứng được).
/// </summary>
public class BlindGrade
{
    public int Id { get; set; }
    public int FundusImageId { get; set; }
    public FundusImage? FundusImage { get; set; }

    public byte Round { get; set; }
    public int GraderId { get; set; }
    public User? Grader { get; set; }

    /// <summary>false = ảnh không chấm được; khi đó Grade = null.</summary>
    public bool Gradable { get; set; }
    /// <summary>Phân độ ICDR 0–4; null khi không chấm được.</summary>
    public DrGrade? Grade { get; set; }
    [MaxLength(500)] public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>1.3.1: null = chấm trong ứng dụng; có giá trị = nhập từ gói chấm offline (máy khác).</summary>
    public int? SourcePackageId { get; set; }
    public GradingPackage? SourcePackage { get; set; }
}
