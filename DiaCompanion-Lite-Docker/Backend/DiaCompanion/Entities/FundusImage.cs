using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class FundusImage : IVoidable, IHasRowVersion    
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public int? VisitId { get; set; }
    public Visit? Visit { get; set; }

    public Eye Eye { get; set; }
    /// <summary>QT-18: đường dẫn TƯƠNG ĐỐI. File ngoài webroot, phục vụ qua endpoint kiểm quyền.</summary>
    [Required, MaxLength(400)] public string FilePath { get; set; } = "";
    [MaxLength(64)] public string? FileSha256 { get; set; }
    public int? SizeBytes { get; set; }
    public short? Width { get; set; }
    public short? Height { get; set; }

    /// <summary>BR-01: chỉ ảnh Gradable mới được đưa vào suy luận.</summary>
    public QualityStatus QualityStatus { get; set; } = QualityStatus.Pending;
    [MaxLength(500)] public string? QualityNote { get; set; }
    public int? QualityCheckedBy { get; set; }
    public DateTime? QualityCheckedAt { get; set; }

    public int UploadedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsVoided { get; set; }
    [MaxLength(500)] public string? VoidReason { get; set; }
    public int? VoidedBy { get; set; }
    public DateTime? VoidedAt { get; set; }

    public byte[] RowVer { get; set; } = Array.Empty<byte>();

    public ICollection<AiDiagnosis> Diagnoses { get; set; } = new List<AiDiagnosis>();
}
