using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class PrescriptionItemInput
{
    private string _drugName = "";
    private string _dose = "";
    private string? _instruction;

    [Required, MaxLength(200)]
    public string DrugName
    {
        get => _drugName;
        set => _drugName = InputText.TrimRequired(value);
    }

    [Required, MaxLength(100)]
    public string Dose
    {
        get => _dose;
        set => _dose = InputText.TrimRequired(value);
    }

    [Range(1, 6)]
    public byte TimesPerDay { get; set; } = 1;

    [Range(1, 365)]
    public short DurationDays { get; set; } = 30;

    [MaxLength(300)]
    public string? Instruction
    {
        get => _instruction;
        set => _instruction = InputText.TrimOptional(value);
    }
}