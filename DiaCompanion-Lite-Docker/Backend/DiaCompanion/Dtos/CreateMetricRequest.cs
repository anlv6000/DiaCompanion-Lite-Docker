using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class CreateMetricRequest
{
    private string? _note;

    [Required]
    public MetricType MetricType { get; set; }

    public decimal Value { get; set; }

    public decimal? SystolicValue { get; set; }

    public decimal? DiastolicValue { get; set; }

    public MetricContext? Context { get; set; }

    /// <summary>
    /// Bỏ trống thì lấy thời điểm hiện tại.
    /// Cho phép ghi bù ngày trước.
    /// </summary>
    public DateTime? RecordedAtUtc { get; set; }

    [MaxLength(300)]
    public string? Note
    {
        get => _note;
        set => _note = InputText.TrimOptional(value);
    }

    // KHÔNG trim
    public string? RowVersion { get; set; }

    /// <summary>
    /// RowVersion của bản ghi còn lại trong cặp huyết áp.
    /// </summary>
    // KHÔNG trim
    public string? PairRowVersion { get; set; }
}