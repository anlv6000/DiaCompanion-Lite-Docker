using DiaCompanion.Api.Common;


namespace DiaCompanion.Api.Dtos;

public class StaffUserDto
{
    
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string Role { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public string? LicenseNo { get; set; }

    /// <summary>Trạng thái staff lấy từ UserRoles.IsActive của Doctor/Receptionist.</summary>
    public bool IsActive { get; set; }
    public string? phone { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RowVersion { get; set; } = "";
}
