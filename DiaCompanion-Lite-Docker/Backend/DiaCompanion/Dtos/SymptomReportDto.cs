using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class SymptomReportDto
{
    public int Id { get; set; }
    public string Symptoms { get; set; } = "";
    public byte Severity { get; set; }
    public string? Description { get; set; }
    public string? OnsetNote { get; set; }
    /// <summary>Do hệ thống sinh ngay lúc gửi, theo mức độ. Bất biến.</summary>
    public string AutoAdvice { get; set; } = "";
    /// <summary>Bác sĩ trả lời sau, trong giờ làm việc. Không ghi đè AutoAdvice.</summary>
    public string? DoctorReply { get; set; }
    public string? RepliedByName { get; set; }
    public DateTime? RepliedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string State { get; set; } = "";
    public string PatientName { get; set; } = "";
    public int? ResponsibleDoctorId { get; set; }
    public string? ResponsibleDoctorName { get; set; }
    public string RowVersion { get; set; } = "";
}
