using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class Prescription : IVoidable, IHasRowVersion
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public int? VisitId { get; set; }
    public int DoctorId { get; set; }
    public User? Doctor { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(1000)] public string? Note { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsVoided { get; set; }
    [MaxLength(500)] public string? VoidReason { get; set; }
    public int? VoidedBy { get; set; }
    public DateTime? VoidedAt { get; set; }

    public byte[] RowVer { get; set; } = Array.Empty<byte>();
    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
}
