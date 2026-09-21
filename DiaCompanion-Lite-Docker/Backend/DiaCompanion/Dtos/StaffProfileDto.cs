using DiaCompanion.Api.Common;


namespace DiaCompanion.Api.Dtos;

/// <summary>Thông tin hồ sơ cá nhân của Doctor/Receptionist đang đăng nhập.</summary>
public class StaffProfileDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = "";
    public string? LicenseNo { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RowVersion { get; set; } = "";
}
