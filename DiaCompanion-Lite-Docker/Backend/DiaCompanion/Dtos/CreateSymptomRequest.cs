using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class CreateSymptomRequest
{
    private string _symptoms = "";
    private string? _description;
    private string? _onsetNote;

    [Required, MaxLength(500)]
    public string Symptoms
    {
        get => _symptoms;
        set => _symptoms = InputText.TrimRequired(value);
    }

    [Required]
    public SymptomSeverity Severity { get; set; }

    [MaxLength(1000)]
    public string? Description
    {
        get => _description;
        set => _description = InputText.TrimOptional(value);
    }

    [MaxLength(100)]
    public string? OnsetNote
    {
        get => _onsetNote;
        set => _onsetNote = InputText.TrimOptional(value);
    }
}