using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class UpdateStaffRequest
{
    private string _fullName = "";
    private string? _licenseNo;
    private string? _role;
    private List<string>? _roles;

    [MaxLength(70, ErrorMessage = "Họ và tên không được vượt quá 70 ký tự.")]
    public string FullName
    {
        get => _fullName;
        set => _fullName = InputText.TrimRequired(value);
    }

    [MaxLength(50, ErrorMessage = "Chứng chỉ không được vượt quá 50 ký tự.")]
    public string? LicenseNo
    {
        get => _licenseNo;
        set => _licenseNo = InputText.TrimOptional(value);
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

    [Required]
    public string RowVersion { get; set; } = "";
}
