using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/// <summary>UC-35: tập ca người–máy mâu thuẫn, phục vụ cải tiến mô hình.</summary>
public class DisagreementCaseDto
{
    public int AiDiagnosisId { get; set; }
    public string PatientCode { get; set; } = "";
    public byte Eye { get; set; }
    public string ModelVersion { get; set; } = "";
    public byte AiGrade { get; set; }
    public byte DoctorGrade { get; set; }
    public int GradeDistance { get; set; }
    public decimal? Disagreement { get; set; }
    public bool WasDeferred { get; set; }
    public string? Reason { get; set; }
    public DateTime ReviewedAt { get; set; }
}
