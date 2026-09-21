using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/* ===================== NHẮC TÁI KHÁM (UC-48) ===================== */

/// <summary>
/// UC-41 — ngày tái khám của bệnh nhân.
///
/// DẪN XUẤT, không lưu trong bảng riêng: ngày = ClosedAt + RecheckMonths của
/// lượt khám hoàn tất gần nhất (BR-19). Hệ thống bỏ chức năng đặt lịch theo
/// khung giờ; bệnh nhân đến khám trực tiếp trong giờ làm việc.
/// </summary>
public class RecheckDto
{
    public int PatientId { get; set; }
    public string PatientCode { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string? PatientPhone { get; set; }

    public int LastVisitId { get; set; }
    public DateTime LastVisitClosedAt { get; set; }
    public byte? LastConfirmedGrade { get; set; }
    public string? LastConfirmedGradeLabel { get; set; }
    public byte? Referral { get; set; }

    public byte RecheckMonths { get; set; }
    public DateOnly DueDate { get; set; }
    /// <summary>Âm là còn hạn, dương là đã quá hạn bấy nhiêu ngày.</summary>
    public int DaysPastDue { get; set; }
    public bool IsOverdue { get; set; }
    public string StatusLabel { get; set; } = "";
}
