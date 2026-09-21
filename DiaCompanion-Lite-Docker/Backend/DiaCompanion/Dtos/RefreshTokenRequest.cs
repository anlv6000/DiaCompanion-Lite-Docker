using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class RefreshTokenRequest
{
    [Required] public string RefreshToken { get; set; } = "";
}
