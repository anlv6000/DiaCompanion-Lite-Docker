
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;    

public class AiDiagnosisDto
{
    public int Id { get; set; }
    public int FundusImageId { get; set; }
    public int? VisitId { get; set; }
    public byte? VisitStatus { get; set; }
    public byte Eye { get; set; }

    // Giữ field cũ để FE hiện tại không vỡ; giá trị là DR model.
    public string ModelVersion { get; set; } = "";

    public int DrModelVersionId { get; set; }
    public string DrModelVersion { get; set; } = "";
    public int? LesionModelVersionId { get; set; }
    public string? LesionModelVersion { get; set; }
    public int? FractalModelVersionId { get; set; }
    public string? FractalModelVersion { get; set; }

    public byte DrGrade { get; set; }
    public string DrGradeLabel { get; set; } = "";
    public byte? ClinicalRiskScore { get; set; }
    public decimal? EffectiveDisagreementThreshold { get; set; }
    public string? ClinicalRiskFactors { get; set; }

    public byte? LesionGradeImplied { get; set; }
    public int? CountMA { get; set; }
    public int? CountHE { get; set; }
    public int? CountEX { get; set; }
    public int? CountSE { get; set; }

    public decimal? Disagreement { get; set; }
    public bool IsDeferred { get; set; }
    public byte? DeferReason { get; set; }
    public string? DeferReasonLabel { get; set; }

    public decimal? FractalDimension { get; set; }
    public decimal? FractalSt { get; set; }
    public decimal? FractalSn { get; set; }
    public decimal? FractalIt { get; set; }
    public decimal? FractalIn { get; set; }
    public decimal? FractalAsymmetry { get; set; }
    public decimal? FractalTn { get; set; }
    public decimal? Lacunarity { get; set; }
    public string? FractalNote { get; set; }
    public bool HasLesionMask { get; set; }
    public bool HasFractalImage { get; set; }

    public DateTime CreatedAt { get; set; }
    public bool IsConfirmed { get; set; }
    public ReviewDto? Review { get; set; }
    public string? RowVersion { get; set; }

}
