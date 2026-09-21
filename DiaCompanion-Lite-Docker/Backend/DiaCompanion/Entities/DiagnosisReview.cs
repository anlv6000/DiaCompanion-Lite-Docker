using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class DiagnosisReview : IVoidable, IHasRowVersion
{
    public int Id { get; set; }
    public int AiDiagnosisId { get; set; }
    public AiDiagnosis? AiDiagnosis { get; set; }
    public int DoctorId { get; set; }
    public User? Doctor { get; set; }

    public ReviewAction Action { get; set; }
    /// <summary>NT-3 / BR-02: chỉ được ghi bởi thao tác này của bác sĩ, không bao giờ bởi AI.</summary>
    public DrGrade FinalGrade { get; set; }
    /// <summary>BR-04: bắt buộc khi Action = Override.</summary>
    [MaxLength(1000)] public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsVoided { get; set; }
    [MaxLength(500)] public string? VoidReason { get; set; }
    public int? VoidedBy { get; set; }
    public DateTime? VoidedAt { get; set; }

    public byte[] RowVer { get; set; } = Array.Empty<byte>();
}
