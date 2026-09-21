using DiaCompanion.Api.Common;


namespace DiaCompanion.Api.Dtos;

public class MedicationLogDto
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public int PrescriptionItemId { get; set; }
    public string DrugName { get; set; } = "";
    public string Dose { get; set; } = "";
    public DateTime ScheduledAt { get; set; }
    public DateTime? TakenAt { get; set; }
    public byte Status { get; set; }
    public string StatusLabel { get; set; } = "";
    public string RowVersion { get; set; } = "";
}
