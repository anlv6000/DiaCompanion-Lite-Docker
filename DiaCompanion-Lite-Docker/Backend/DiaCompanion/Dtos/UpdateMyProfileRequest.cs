using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

/// <summary>
/// Bệnh nhân tự cập nhật thông tin cá nhân không nhạy cảm.
/// Số điện thoại được đổi qua luồng OTP riêng; dữ liệu lâm sàng không được sửa tại đây.
/// </summary>
public class UpdateMyProfileRequest
{

    private string _fullName = "";
    private string? _address;

    [Required, MaxLength(70, ErrorMessage = "Họ và tên không được vượt quá 70 ký tự.")]
    public string FullName
    {
        get => _fullName;
        set => _fullName = InputText.TrimRequired(value);
    }

    [Range(0, 2, ErrorMessage = "Giới tính không hợp lệ.")]
    public byte Gender { get; set; }

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [MaxLength(300)]
    public string? Address
    {
        get => _address;
        set => _address = InputText.TrimOptional(value);
    }

    [Required]
    public string RowVersion { get; set; } = "";
}
