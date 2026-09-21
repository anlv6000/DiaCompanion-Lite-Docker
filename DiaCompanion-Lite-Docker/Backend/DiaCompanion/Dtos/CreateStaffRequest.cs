using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class CreateStaffRequest
{
    private string _phone = "";
    private string _email = "";
    private string _fullName = "";
    private string? _role;
    private string? _licenseNo;
    private List<string>? _roles;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(
        @"^\d{10,11}$",
        ErrorMessage = "Số điện thoại phải gồm 10 đến 11 chữ số.")]
    public string Phone
    {
        get => _phone;
        set => _phone = InputText.TrimRequired(value);
    }

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email
    {
        get => _email;
        set => _email = InputText.TrimRequired(value);
    }

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [MaxLength(70, ErrorMessage = "Họ tên không được vượt quá 70 ký tự.")]
    public string FullName
    {
        get => _fullName;
        set => _fullName = InputText.TrimRequired(value);
    }

    public string? Role
    {
        get => _role;
        set => _role = InputText.TrimOptional(value);
    }

    public List<string>? Roles
    {
        get => _roles;
        set => _roles = value?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
    }

    [MaxLength(50)]
    public string? LicenseNo
    {
        get => _licenseNo;
        set => _licenseNo = InputText.TrimOptional(value);
    }
}
