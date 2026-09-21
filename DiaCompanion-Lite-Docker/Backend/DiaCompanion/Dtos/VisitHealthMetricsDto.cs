using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/// <summary>Bộ chỉ số sức khỏe được ghi nhận trong một lượt khám.</summary>
public class VisitHealthMetricsDto
{
    public int VisitId { get; set; }
    public HealthMetricDto? Glucose { get; set; }
    public HealthMetricDto? HbA1c { get; set; }
    /// <summary>Dùng HealthMetricDto với MetricType=SystolicBp; Diastolic nằm ở Pair/SystolicValue/DiastolicValue.</summary>
    public HealthMetricDto? BloodPressure { get; set; }
}

/// <summary>
/// PUT toàn bộ form chỉ số của lượt khám.
/// Giá trị null nghĩa là lượt khám không có chỉ số đó; nếu trước đó đã có thì bản ghi sẽ được soft-delete.
/// </summary>
public class SaveVisitHealthMetricsRequest
{
    public decimal? Glucose { get; set; }
    public MetricContext? GlucoseContext { get; set; }
    [MaxLength(300)] public string? GlucoseNote { get; set; }
    public string? GlucoseRowVersion { get; set; }

    public decimal? HbA1c { get; set; }
    [MaxLength(300)] public string? HbA1cNote { get; set; }
    public string? HbA1cRowVersion { get; set; }

    public decimal? SystolicBp { get; set; }
    public decimal? DiastolicBp { get; set; }
    [MaxLength(300)] public string? BloodPressureNote { get; set; }
    public string? SystolicRowVersion { get; set; }
    public string? DiastolicRowVersion { get; set; }
}
