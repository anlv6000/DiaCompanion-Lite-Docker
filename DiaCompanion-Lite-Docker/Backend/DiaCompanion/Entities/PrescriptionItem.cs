using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class PrescriptionItem
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }
    [Required, MaxLength(200)] public string DrugName { get; set; } = "";
    [Required, MaxLength(100)] public string Dose { get; set; } = "";
    public byte TimesPerDay { get; set; }
    public short DurationDays { get; set; }
    [MaxLength(300)] public string? Instruction { get; set; }
    /// <summary>Không xoá cứng dòng thuốc đã có lịch sử dùng thuốc; chỉ ngừng hiệu lực.</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<MedicationLog> Logs { get; set; } = new List<MedicationLog>();
}
