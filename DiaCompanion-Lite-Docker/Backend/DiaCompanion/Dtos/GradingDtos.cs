using System.ComponentModel.DataAnnotations;

namespace DiaCompanion.Api.Dtos;

/// <summary>Một lần chấm mù: phân độ ICDR 0–4, hoặc "không chấm được".</summary>
public class BlindGradeRequest
{
    public bool Gradable { get; set; }

    [Range(0, 4, ErrorMessage = "Phân độ phải từ 0 đến 4.")]
    public byte? Grade { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class BlindGradeDto
{
    public int ImageId { get; set; }
    public byte Round { get; set; }
    public bool Gradable { get; set; }
    public byte? Grade { get; set; }
    public string? GradeLabel { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GradingQueueItemDto
{
    /// <summary>Id ảnh — không mang thông tin bệnh nhân. Người chấm chỉ thấy số thứ tự.</summary>
    public int ImageId { get; set; }
    public int Position { get; set; }
}

public class GradingQueueDto
{
    public byte Round { get; set; }
    public int Remaining { get; set; }
    public List<GradingQueueItemDto> Items { get; set; } = new();
}

public class GradingProgressDto
{
    /// <summary>Số ảnh đã có vòng 1 (bác sĩ khám chấm mù).</summary>
    public int Round1 { get; set; }
    public int Round1Ungradable { get; set; }
    public int Round2Done { get; set; }
    /// <summary>Ảnh chấm được ở vòng 1 nhưng chưa có vòng 2.</summary>
    public int Round2Pending { get; set; }
    /// <summary>Ảnh vòng 1 và 2 bất đồng ở ngưỡng chuyển tuyến hoặc khả năng chấm.</summary>
    public int Round3Needed { get; set; }
    public int Round3Done { get; set; }
    /// <summary>1.3.1: ảnh đang nằm trong gói chấm còn mở (đang được chấm ở máy khác).</summary>
    public int Round2InPackages { get; set; }
    public int Round3InPackages { get; set; }
}

/// <summary>
/// Thông tin lâm sàng hiển thị cạnh ảnh (trang AI, chấm mù vòng 1/2/3).
/// Với người chấm độc lập: không trả mã nghiên cứu và ngày khám.
/// </summary>
public class ImageClinicalContextDto
{
    public int ImageId { get; set; }
    public byte Eye { get; set; }
    public string? ResearchCode { get; set; }
    public DateTime? VisitDate { get; set; }
    public int BirthYear { get; set; }
    public int? AgeAtVisit { get; set; }
    public byte Gender { get; set; }
    public byte DiabetesType { get; set; }
    public short? DiabetesDurationYears { get; set; }
    public decimal? BaselineHbA1c { get; set; }
    public string? PatientNote { get; set; }
    public decimal? HbA1c { get; set; }
    public decimal? SystolicBp { get; set; }
    public decimal? DiastolicBp { get; set; }
    public decimal? Glucose { get; set; }
    public byte? GlucoseContext { get; set; }
}
