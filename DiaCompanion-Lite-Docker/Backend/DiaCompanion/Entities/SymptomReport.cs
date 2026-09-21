using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class SymptomReport : ISoftDeletable, IHasRowVersion
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    /// <summary>Bác sĩ phụ trách tại thời điểm bệnh nhân gửi báo cáo.</summary>
    public int? ResponsibleDoctorId { get; set; }
    public User? ResponsibleDoctor { get; set; }
    [Required, MaxLength(500)] public string Symptoms { get; set; } = "";
    public SymptomSeverity Severity { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
    [MaxLength(100)] public string? OnsetNote { get; set; }

    /// <summary>
    /// Khuyến cáo do HỆ THỐNG sinh ngay khi gửi, theo mức độ. Bất biến.
    /// Tách khỏi DoctorReply vì một cột duy nhất sẽ khiến trả lời của bác sĩ
    /// ghi đè khuyến cáo tự động — mất vết nguồn gốc, không chấp nhận được
    /// với dữ liệu y tế.
    /// </summary>
    [Required, MaxLength(1000)] public string AutoAdvice { get; set; } = "";

    [MaxLength(1000)] public string? DoctorReply { get; set; }
    public int? RepliedBy { get; set; }
    public DateTime? RepliedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public byte[] RowVer { get; set; } = Array.Empty<byte>();
}
