using System.ComponentModel.DataAnnotations;

namespace DiaCompanion.Api.Entities;

/// <summary>
/// Vai trò được cấu hình trong bảng dbo.Roles. Mã Id chỉ là khóa ngoại của DB;
/// tầng ứng dụng nhận diện vai trò bằng Name để không phụ thuộc RoleId cố định.
/// </summary>
public sealed class Role
{
    public byte Id { get; set; }
    [Required, MaxLength(50)] public string Name { get; set; } = "";
    [Required, MaxLength(100)] public string DisplayName { get; set; } = "";
    [MaxLength(300)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
