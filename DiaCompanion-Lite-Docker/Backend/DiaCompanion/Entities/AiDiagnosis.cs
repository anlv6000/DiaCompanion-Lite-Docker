using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class AiDiagnosis : IVoidable, IHasRowVersion
{
    public int Id { get; set; }
    public int FundusImageId { get; set; }
    public FundusImage? FundusImage { get; set; }

    // ModelVersionId được GIỮ LẠI để tương thích schema/API cũ và từ bây giờ
    // đại diện cho model DR. Hai model còn lại có FK riêng.
    public int ModelVersionId { get; set; }
    public ModelVersion? ModelVersion { get; set; }

    public int? LesionModelVersionId { get; set; }
    public ModelVersion? LesionModelVersion { get; set; }

    public int? FractalModelVersionId { get; set; }
    public ModelVersion? FractalModelVersion { get; set; }

    // ---- nhánh phân loại (DR model) ----
    public DrGrade DrGrade { get; set; }
    public decimal Confidence { get; set; }
    [MaxLength(200)] public string? GradeProbabilities { get; set; }

    // ---- nhánh phân vùng tổn thương (Lesion model) ----
    public DrGrade? LesionGradeImplied { get; set; }
    [MaxLength(400)] public string? LesionMaskPath { get; set; }
    public int? CountMA { get; set; }
    public int? CountHE { get; set; }
    public int? CountEX { get; set; }
    public int? CountSE { get; set; }
    public decimal? AreaMA { get; set; }
    public decimal? AreaHE { get; set; }
    public decimal? AreaEX { get; set; }
    public decimal? AreaSE { get; set; }

    // ---- Gap 2: tín hiệu bất đồng chéo ----
    public decimal? Disagreement { get; set; }
    public decimal? EffectiveDisagreementThreshold { get; set; }
    public byte? ClinicalRiskScore { get; set; }
    [MaxLength(500)] public string? ClinicalRiskFactors { get; set; }
    public bool IsDeferred { get; set; }
    public DeferReason? DeferReason { get; set; }
    public decimal? ConfidenceThreshold { get; set; }
    public decimal? DisagreementThreshold { get; set; }

    // ---- Fractal model ----
    // ---- Gap 3: Sectorial Fractal Analysis ----
    // FD_total tính trên toàn mask mạch máu, giữ để đối chiếu với y văn.
    public decimal? FractalDimension { get; set; }

    // FD từng góc phần tư, đã chuẩn hoá theo mắt (ảnh OS được lật ngang trước
    // khi chia) và đã loại vùng đĩa thị.
    // KHÔNG so sánh trực tiếp với FractalDimension: dải hộp và diện tích khác
    // nhau nên hai nhóm giá trị nằm trên hai thang khác nhau.
    public decimal? FractalSt { get; set; }   // superotemporal
    public decimal? FractalSn { get; set; }   // superonasal
    public decimal? FractalIt { get; set; }   // inferotemporal
    public decimal? FractalIn { get; set; }   // inferonasal

    // Độ lệch chuẩn của bốn giá trị trên — chỉ dấu chính của hướng nghiên cứu.
    // Là tỉ số nội ảnh nên ổn định hơn FD tuyệt đối khi so theo thời gian.
    public decimal? FractalAsymmetry { get; set; }

    // Chênh lệch temporal trừ nasal, có dấu. Dương = phía thái dương phức tạp hơn.
    public decimal? FractalTn { get; set; }

    public decimal? Lacunarity { get; set; }

    [MaxLength(400)] public string? VesselMaskPath { get; set; }
    [MaxLength(300)] public string? FractalNote { get; set; }

    public int? InferenceMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsVoided { get; set; }
    [MaxLength(500)] public string? VoidReason { get; set; }
    public int? VoidedBy { get; set; }
    public DateTime? VoidedAt { get; set; }
    public int? LastReviewActionBy { get; set; }
    public DateTime? LastReviewActionAt { get; set; }

    public byte[]? RowVer { get; set; }

    public ICollection<DiagnosisReview> Reviews { get; set; } = new List<DiagnosisReview>();
}
