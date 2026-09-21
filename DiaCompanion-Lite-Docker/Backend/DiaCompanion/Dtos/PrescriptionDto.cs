using DiaCompanion.Api.Common;



namespace DiaCompanion.Api.Dtos;

/* ========================= PRESCRIPTION (UC-36..40) ===================== */

public class PrescriptionDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int? VisitId { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Note { get; set; }
    public bool IsVoided { get; set; }
    public string? VoidReason { get; set; }
    public DateTime? VoidedAt { get; set; }
    public int ScheduledDoses { get; set; }
    public int TakenDoses { get; set; }
    public int MissedDoses { get; set; }
    public int SkippedDoses { get; set; }
    public decimal AdherenceRate { get; set; }
    public List<PrescriptionItemDto> Items { get; set; } = new();
    public string RowVersion { get; set; } = "";
}
