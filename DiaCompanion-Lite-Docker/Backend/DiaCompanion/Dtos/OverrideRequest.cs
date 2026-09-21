using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class OverrideRequest : ReviewRequest
{
    private string _reason = "";

    [Required]
    public DrGrade FinalGrade { get; set; }

    /// <summary>
    /// BR-04: bắt buộc.
    /// </summary>
    [Required, MaxLength(1000)]
    public string Reason
    {
        get => _reason;
        set => _reason = InputText.TrimRequired(value);
    }
}