using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Dtos;

public class UpdatePrescriptionRequest
{
    private string? _note;

    [MaxLength(1000)]
    public string? Note
    {
        get => _note;
        set => _note = InputText.TrimOptional(value);
    }

    // KHÔNG trim RowVersion
    [Required]
    public string RowVersion { get; set; } = "";

    [Required, MinLength(1)]
    public List<UpdatePrescriptionItemRequest> Items { get; set; } = [];
}

public class UpdatePrescriptionItemRequest
{
    private string _drugName = "";
    private string _dose = "";
    private string? _instruction;

    /// <summary>
    /// ID hiện có; gửi 0 để thêm dòng thuốc mới.
    /// </summary>
    public int Id { get; set; }

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
    public byte TimesPerDay { get; set; }

    [Range(1, 365)]
    public int DurationDays { get; set; }

    [MaxLength(300)]
    public string? Instruction
    {
        get => _instruction;
        set => _instruction = InputText.TrimOptional(value);
    }
}