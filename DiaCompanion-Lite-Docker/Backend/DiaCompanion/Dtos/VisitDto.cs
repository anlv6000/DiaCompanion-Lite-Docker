using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/* ============================ VISITS (UC-18..21) ========================= */

public class VisitDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = "";
    public string PatientCode { get; set; } = "";
    public int? DoctorId { get; set; }
    public string? DoctorName { get; set; }
    public DateTime VisitDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte Status { get; set; }
    public string? Conclusion { get; set; }
    public byte? Referral { get; set; }
    public byte? RecheckMonths { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int ImageCount { get; set; }
    public int PendingReviewCount { get; set; }
    public VisitHealthMetricsDto? HealthMetrics { get; set; }

    // Chỉ được populate ở GET /api/visits/me/{id} để Patient xem kết quả đã
    // được bác sĩ xác nhận. Các endpoint Visit dùng cho staff không cần tải
    // thêm review/đơn thuốc ở mỗi request.
    public List<VisitConfirmedFindingDto>? ConfirmedFindings { get; set; }
    public List<VisitPrescriptionDto>? Prescriptions { get; set; }

    public string RowVersion { get; set; } = "";
}

/// <summary>
/// Kết quả võng mạc đã được bác sĩ xác nhận cho một mắt trong lượt khám.
/// Nếu có nhiều ảnh/review của cùng một mắt, backend chọn mức nặng nhất để
/// hiển thị cho Patient, đồng nhất với nguyên tắc lấy mắt nặng hơn khi theo dõi.
/// </summary>
public class VisitConfirmedFindingDto
{
    public byte Eye { get; set; }
    public byte FinalGrade { get; set; }
    public string FinalGradeLabel { get; set; } = "";
    public string? ConfirmedBy { get; set; }
    public DateTime ConfirmedAt { get; set; }
}

/// <summary>Đơn thuốc read-only thuộc đúng lượt khám đang được Patient xem.</summary>
public class VisitPrescriptionDto
{
    public int Id { get; set; }
    public DateTime IssuedAt { get; set; }
    public string? Note { get; set; }
    public List<VisitPrescriptionItemDto> Items { get; set; } = new();
}

/// <summary>Thông tin dùng thuốc cần thiết cho Patient; không chứa dữ liệu quản trị.</summary>
public class VisitPrescriptionItemDto
{
    public int Id { get; set; }
    public string DrugName { get; set; } = "";
    public string Dose { get; set; } = "";
    public byte TimesPerDay { get; set; }
    public short DurationDays { get; set; }
    public string? Instruction { get; set; }
}
