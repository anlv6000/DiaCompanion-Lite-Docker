using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class Notification
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public NotificationType Type { get; set; }
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    [Required, MaxLength(500)] public string Message { get; set; } = "";
    [MaxLength(50)] public string? LinkEntity { get; set; }
    public int? LinkEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}
