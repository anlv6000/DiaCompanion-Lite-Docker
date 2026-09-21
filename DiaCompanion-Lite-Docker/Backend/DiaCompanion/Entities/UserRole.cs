namespace DiaCompanion.Api.Entities;

/// <summary>Quan hệ nhiều-nhiều Users &lt;-&gt; Roles, có trạng thái kích hoạt riêng.</summary>
public sealed class UserRole
{
    public int UserId { get; set; }
    public byte RoleId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public int? AssignedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
    public User? AssignedByUser { get; set; }
}
