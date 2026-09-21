using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class UpdateConfigRequest
{
    private string _value = "";

    [Required]
    public string Value
    {
        get => _value;
        set => _value = InputText.TrimRequired(value);
    }

    // KHÔNG trim RowVersion
    [Required]
    public string RowVersion { get; set; } = "";
}