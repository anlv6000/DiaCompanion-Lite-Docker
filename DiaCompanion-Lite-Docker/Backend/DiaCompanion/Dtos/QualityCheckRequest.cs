using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class QualityCheckRequest
{
    private string? _note;

    [Required]
    public QualityStatus Status { get; set; }

    /// <summary>
    /// Bắt buộc khi Status = Ungradable.
    /// </summary>
    [MaxLength(500)]
    public string? Note
    {
        get => _note;
        set => _note = InputText.TrimOptional(value);
    }

    // KHÔNG trim RowVersion
    [Required]
    public string RowVersion { get; set; } = "";
}