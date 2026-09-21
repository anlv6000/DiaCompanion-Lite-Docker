using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/* ========================== WORKFLOW (UC-30..35) ======================== */

public class TriageItemDto
{
    public int AiDiagnosisId { get; set; }
    public int PatientId { get; set; }
    public string PatientCode { get; set; } = "";
    public string PatientName { get; set; } = "";
    public int? VisitId { get; set; }
    public byte Eye { get; set; }
    public byte DrGrade { get; set; }
    public byte? ClinicalRiskScore { get; set; }
    public decimal? Disagreement { get; set; }
    public bool IsDeferred { get; set; }
    public byte? DeferReason { get; set; }
    public bool NeedsReferral { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? DoctorName { get; set; }
    public string? RowVersion { get; set; }
}
