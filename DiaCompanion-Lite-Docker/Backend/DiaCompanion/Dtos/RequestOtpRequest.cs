using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class RequestOtpRequest
{
    private string _phone = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(
    @"^\d{10,11}$",
    ErrorMessage = "Số điện thoại phải gồm 10 đến 11 chữ số.")]
    public string Phone
    {
        get => _phone;
        set => _phone = InputText.TrimRequired(value);
    }
}
