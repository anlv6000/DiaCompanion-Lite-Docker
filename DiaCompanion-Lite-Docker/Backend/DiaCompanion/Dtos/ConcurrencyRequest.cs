using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/// <summary>Version token returned by the last GET response.</summary>
public class ConcurrencyRequest
{
    [Required]
    public string RowVersion { get; set; } = "";
    /// <summary>Token của bản ghi liên quan khi thao tác một aggregate gồm hai dòng (ví dụ huyết áp).</summary>
    public string? PairRowVersion { get; set; }
}
