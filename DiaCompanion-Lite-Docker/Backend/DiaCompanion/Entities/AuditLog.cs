using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    /// <summary>Chụp lại tên tại thời điểm thao tác — tài khoản đổi tên sau không làm sai vết cũ.</summary>
    [MaxLength(200)] public string? UserName { get; set; }
    [Required, MaxLength(50)] public string Action { get; set; } = "";
    [Required, MaxLength(50)] public string EntityType { get; set; } = "";
    public int? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    [MaxLength(1000)] public string? Detail { get; set; }
    [MaxLength(45)] public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
