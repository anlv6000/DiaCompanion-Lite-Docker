using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/* =========================== IMAGING (UC-22..29) ======================== */

public class FundusImageDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int? VisitId { get; set; }
    public byte Eye { get; set; }
    public byte QualityStatus { get; set; }
    public string? QualityNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ContentUrl { get; set; }
    public AiDiagnosisDto? LatestDiagnosis { get; set; }

    public string RowVersion { get; set; } = "";
}
