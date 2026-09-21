using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

/// <summary>
/// Doctor/Receptionist tự cập nhật hồ sơ cá nhân.
/// Email và role không được đổi tại đây vì là thông tin đăng nhập/phân quyền do Admin quản lý.
/// </summary>
public class UpdateStaffProfileRequest
{
    private string _fullName = "";
    private string _phone = "";
    private string? _licenseNo;

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [MaxLength(70, ErrorMessage = "Họ và tên không được vượt quá 70 ký tự.")]
    public string FullName
    {
        get => _fullName;
        set => _fullName = InputText.TrimRequired(value);
    }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(
        @"^\d{10,11}$",
        ErrorMessage = "Số điện thoại phải gồm 10 đến 11 chữ số.")]
    public string Phone
    {
        get => _phone;
        set => _phone = InputText.TrimRequired(value);
    }

    [MaxLength(
        50,
        ErrorMessage = "Số chứng chỉ hành nghề không được vượt quá 50 ký tự.")]
    public string? LicenseNo
    {
        get => _licenseNo;
        set => _licenseNo = InputText.TrimOptional(value);
    }

    // KHÔNG trim RowVersion
    [Required(
        ErrorMessage = "Thiếu phiên bản dữ liệu. Vui lòng tải lại trang.")]
    public string RowVersion { get; set; } = "";
}