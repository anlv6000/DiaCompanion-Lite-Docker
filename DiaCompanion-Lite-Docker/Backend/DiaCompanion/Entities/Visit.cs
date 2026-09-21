using DiaCompanion.Api.Common;
using DiaCompanion.Entities;
using System.ComponentModel.DataAnnotations;

namespace DiaCompanion.Api.Entities;

public class Visit : IVoidable, IHasRowVersion
{
    public int Id { get; set; }
    //public int PatientId { get; set; }
    //public Patient? Patient { get; set; }
    /// <summary>"Bác sĩ phụ trách" của bệnh nhân = bác sĩ của lượt khám gần nhất.</summary>
    public int? DoctorId { get; set; }
    public User? Doctor { get; set; }

    public DateTime VisitDate { get; set; } = DateTime.UtcNow;
    public VisitStatus Status { get; set; } = VisitStatus.InProgress;
    [MaxLength(2000)] public string? Conclusion { get; set; }
    public ReferralType? Referral { get; set; }
    /// <summary>BR-19: chu kỳ tái tầm soát, suy từ mức DR đã xác nhận.</summary>
    public byte? RecheckMonths { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsVoided { get; set; }
    [MaxLength(500)] public string? VoidReason { get; set; }
    public int? VoidedBy { get; set; }
    public DateTime? VoidedAt { get; set; }
    public byte[] RowVer { get; set; } = Array.Empty<byte>();
    public ICollection<FundusImage> Images { get; set; } = new List<FundusImage>();
    public ICollection<HealthMetric> HealthMetrics { get; set; } = new List<HealthMetric>();

    public int MedicalRecordId { get; set; }

    public MedicalRecord MedicalRecord { get; set; } = null!;
}
