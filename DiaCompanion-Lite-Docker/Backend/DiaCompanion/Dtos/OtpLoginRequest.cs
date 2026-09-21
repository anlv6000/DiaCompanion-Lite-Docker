using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

/// <summary>
/// UC-01 phương thức 2:
/// bệnh nhân đăng nhập bằng OTP.
/// </summary>
public class OtpLoginRequest
{
    private string _phone = "";
    private string _code = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(
     @"^\d{10,11}$",
     ErrorMessage = "Số điện thoại phải gồm 10 đến 11 chữ số.")]
    public string Phone
    {
        get => _phone;
        set => _phone = InputText.TrimRequired(value);
    }
    [Required]
    public string Code
    {
        get => _code;
        set => _code = InputText.TrimRequired(value);
    }
}