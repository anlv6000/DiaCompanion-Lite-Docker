using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class CreatePrescriptionRequest
{
    private string? _note;

    [Required]
    public int PatientId { get; set; }

    public int? VisitId { get; set; }

    [MaxLength(1000)]
    public string? Note
    {
        get => _note;
        set => _note = InputText.TrimOptional(value);
    }

    [Required, MinLength(1)]
    public List<PrescriptionItemInput> Items { get; set; } = new();
}