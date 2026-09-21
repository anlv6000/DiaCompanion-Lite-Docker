using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class HealthMetric : ISoftDeletable, IHasRowVersion
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Null nếu bệnh nhân tự ghi ngoài lượt khám; có giá trị nếu bác sĩ ghi trong visit.</summary>
    public int? VisitId { get; set; }
    public Visit? Visit { get; set; }

    public MetricType MetricType { get; set; }
    public decimal Value { get; set; }
    [Required, MaxLength(20)] public string Unit { get; set; } = "";
    public MetricContext? Context { get; set; }

    public DateTime RecordedAtUtc { get; set; }
    /// <summary>
    /// QT-10: chỉ số "trước ăn sáng" đo 06:45 giờ VN là 23:45 UTC HÔM TRƯỚC.
    /// Gom theo ngày UTC sẽ làm lệch biểu đồ ngày và tỉ lệ tuân thủ 30 ngày.
    /// </summary>
    public DateOnly RecordedLocalDate { get; set; }

    [MaxLength(300)] public string? Note { get; set; }
    public bool IsAbnormal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public byte[] RowVer { get; set; } = Array.Empty<byte>();
}
