using DiaCompanion.Api.Common;
using DiaCompanion.Entities;
using System.ComponentModel.DataAnnotations;

namespace DiaCompanion.Api.Entities;

public class Patient : IVoidable, IHasRowVersion
{
    public int Id { get; set; }
    [Required, MaxLength(20)] public string Code { get; set; } = "";
    public int? UserId { get; set; }
    public User? User { get; set; }

    [Required, MaxLength(70)] public string FullName { get; set; } = "";
    /// <summary>QT-15: bản bỏ dấu, sinh tự động khi lưu. Có index riêng.</summary>
    [MaxLength(70)] public string? FullNameSearch { get; set; }
    public byte Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    [Required, MaxLength(20)] public string Phone { get; set; } = "";
    [MaxLength(300)] public string? Address { get; set; }

    // Bệnh gốc — yếu tố nguy cơ của biến chứng võng mạc
    public byte DiabetesType { get; set; } = 2;
    public short? DiabetesDurationYears { get; set; }
    public decimal? BaselineHbA1c { get; set; }
    [MaxLength(1000)] public string? Note { get; set; }

    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public bool IsVoided { get; set; }
    [MaxLength(500)] public string? VoidReason { get; set; }
    public int? VoidedBy { get; set; }
    public DateTime? VoidedAt { get; set; }

    public ICollection<FundusImage> Images { get; set; } = new List<FundusImage>();
    public byte[] RowVer { get; set; } = Array.Empty<byte>();

    public ICollection<MedicalRecord> MedicalRecords { get; set; }
    = new List<MedicalRecord>();
}
