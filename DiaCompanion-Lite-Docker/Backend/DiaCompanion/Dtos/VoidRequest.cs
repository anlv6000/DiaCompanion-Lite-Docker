using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class VoidRequest
{
    private string _reason = "";

    [Required, MaxLength(500)]
    public string Reason
    {
        get => _reason;
        set => _reason = InputText.TrimRequired(value);
    }

    // KHÔNG trim RowVersion
    [Required]
    public string RowVersion { get; set; } = "";
}