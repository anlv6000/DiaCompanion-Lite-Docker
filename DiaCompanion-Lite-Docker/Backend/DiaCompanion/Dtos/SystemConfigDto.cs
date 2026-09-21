using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class SystemConfigDto
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string ValueType { get; set; } = "";
    public string? Description { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string RowVersion { get; set; } = "";
}
