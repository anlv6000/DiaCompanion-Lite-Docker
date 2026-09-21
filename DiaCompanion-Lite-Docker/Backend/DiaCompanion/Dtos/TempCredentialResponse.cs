using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class TempCredentialResponse
{
    public string LoginId { get; set; } = "";
    public string TempPassword { get; set; } = "";
    public string Note { get; set; } = "";

    public string? RowVersion { get; set; }
}
