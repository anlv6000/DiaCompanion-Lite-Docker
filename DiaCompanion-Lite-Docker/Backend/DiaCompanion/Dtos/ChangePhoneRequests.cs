using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class RequestPhoneChangeOtpRequest
{
    private string _newPhone = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(
    @"^\d{10,11}$",
    ErrorMessage = "Số điện thoại phải gồm 10 đến 11 chữ số.")]
    public string NewPhone
    {
        get => _newPhone;
        set => _newPhone = InputText.TrimRequired(value);
    }
}

public class ConfirmPhoneChangeRequest
{
    private string _newPhone = "";
    private string _code = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(
    @"^\d{10,11}$",
    ErrorMessage = "Số điện thoại phải gồm 10 đến 11 chữ số.")]
    public string NewPhone
    {
        get => _newPhone;
        set => _newPhone = InputText.TrimRequired(value);
    }

    [Required, StringLength(6, MinimumLength = 6)]
    public string Code
    {
        get => _code;
        set => _code = InputText.TrimRequired(value);
    }

    // KHÔNG trim RowVersion
    [Required]
    public string RowVersion { get; set; } = "";
}