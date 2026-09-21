using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class DoctorReplyRequest
{
    private string _reply = "";

    [Required, MaxLength(1000)]
    public string Reply
    {
        get => _reply;
        set => _reply = InputText.TrimRequired(value);
    }

    // KHÔNG trim RowVersion
    [Required]
    public string RowVersion { get; set; } = "";
}