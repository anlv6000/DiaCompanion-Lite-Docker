using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class MedicationLog : IHasRowVersion
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int PrescriptionItemId { get; set; }
    public PrescriptionItem? PrescriptionItem { get; set; }
    public DateTime ScheduledAt { get; set; }
    /// <summary>QT-10: gom "hôm nay" theo ngày địa phương, không theo ngày UTC.</summary>
    public DateOnly ScheduledLocalDate { get; set; }
    public DateTime? TakenAt { get; set; }
    /// <summary>Thời điểm hệ thống đã tạo thông báo nhắc cho liều này.</summary>
    public DateTime? ReminderSentAt { get; set; }
    public MedicationStatus Status { get; set; } = MedicationStatus.Pending;    

    public byte[] RowVer { get; set; } = Array.Empty<byte>();
}
