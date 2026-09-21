using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/* ========================= ENGAGEMENT (UC-49..52) ======================= */

public class NotificationDto
{
    public long Id { get; set; }
    public byte Type { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? LinkEntity { get; set; }
    public int? LinkEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
