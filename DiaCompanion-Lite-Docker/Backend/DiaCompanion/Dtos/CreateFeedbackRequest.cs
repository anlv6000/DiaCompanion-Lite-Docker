using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class CreateFeedbackRequest
{
    private string? _tags;
    private string? _comment;

    public int? VisitId { get; set; }

    [Range(1, 5)]
    public byte Rating { get; set; }

    [MaxLength(300)]
    public string? Tags
    {
        get => _tags;
        set => _tags = InputText.TrimOptional(value);
    }

    [MaxLength(1000)]
    public string? Comment
    {
        get => _comment;
        set => _comment = InputText.TrimOptional(value);
    }
}