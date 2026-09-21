using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

/* ============================== AUTH (UC-01..05) ========================== */

/// <summary>UC-01. Nhân viên đăng nhập bằng email; bệnh nhân bằng số điện thoại.</summary>
public class LoginRequest
{
    //public string? Email { get; set; }
    //public string? Phone { get; set; }
    //[Required] public string Password { get; set; } = "";
    private string? _email;
    private string? _phone;

    public string? Email
    {
        get => _email;
        set => _email = InputText.TrimOptional(value);
    }
    [RegularExpression(
    @"^\d{10,11}$",
    ErrorMessage = "Số điện thoại phải gồm 10 đến 11 chữ số.")]
    public string? Phone
    {
        get => _phone;
        set => _phone = InputText.TrimOptional(value);
    }

    // TUYỆT ĐỐI KHÔNG trim password
    [Required]
    public string Password { get; set; } = "";
}
