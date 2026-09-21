using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class SystemConfig: IHasRowVersion
{
    /// <summary>QT-19: chỉ tham số nghiệp vụ. Secret nằm ở biến môi trường.</summary>
    [Key, MaxLength(100)] public string Key { get; set; } = "";
    [Required, MaxLength(500)] public string Value { get; set; } = "";
    [MaxLength(20)] public string ValueType { get; set; } = "string";
    [MaxLength(500)] public string? Description { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVer { get; set; } = Array.Empty<byte>();
}
