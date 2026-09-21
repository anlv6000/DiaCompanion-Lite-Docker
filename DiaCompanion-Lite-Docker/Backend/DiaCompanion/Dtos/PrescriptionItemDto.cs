using DiaCompanion.Api.Common;


namespace DiaCompanion.Api.Dtos;

public class PrescriptionItemDto
{
    public int Id { get; set; }
    public string DrugName { get; set; } = "";
    public string Dose { get; set; } = "";
    public byte TimesPerDay { get; set; }
    public short DurationDays { get; set; }
    public string? Instruction { get; set; }
    public bool IsActive { get; set; }
}
