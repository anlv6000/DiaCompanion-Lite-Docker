using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class ModelVersion : IHasRowVersion
{
    public int Id { get; set; }

    /// <summary>
    /// Một lần chạy AI cần đúng 3 loại model active: DR, Lesion và Fractal.
    /// Mỗi loại được quản lý version/activate độc lập.
    /// </summary>
    public ModelType ModelType { get; set; }

    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [Required, MaxLength(400)] public string FilePath { get; set; } = "";
    /// <summary>QT-20: verify lúc nạp. Trả lời được "làm sao biết file model không bị tráo?".</summary>
    [Required, MaxLength(64)] public string Sha256 { get; set; } = "";
    public decimal? Qwk { get; set; }
    public decimal? Dice { get; set; }
    public decimal? IoU { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public bool IsActive { get; set; }
    /// <summary>BR-16: đã từng kích hoạt thì cấm xoá, vì có kết quả tham chiếu tới.</summary>
    public bool WasActivated { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public byte[] RowVer { get; set; } = Array.Empty<byte>();
}
