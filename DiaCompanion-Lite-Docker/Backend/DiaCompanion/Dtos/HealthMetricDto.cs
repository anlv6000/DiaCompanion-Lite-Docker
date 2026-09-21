using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/* ========================= MONITORING (UC-41..47) ======================= */

public class HealthMetricDto
{
    public int Id { get; set; }
    public int? VisitId { get; set; }
    public byte MetricType { get; set; }
    public decimal Value { get; set; }
    public string Unit { get; set; } = "";
    public byte? Context { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public DateOnly RecordedLocalDate { get; set; }
    public string? Note { get; set; }
    public bool IsAbnormal { get; set; }

    public string RowVersion { get; set; } = "";
    public int? PairMetricId { get; set; }
    public string? PairRowVersion { get; set; }
    public decimal? SystolicValue { get; set; }
    public decimal? DiastolicValue { get; set; }
}
public class MetricLatestDto
{
    public int Id { get; set; }
    public int? VisitId { get; set; }
    public byte MetricType { get; set; }
    public decimal Value { get; set; }
    public string Unit { get; set; } = "";
    public byte? Context { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public DateOnly RecordedLocalDate { get; set; }
    public bool IsAbnormal { get; set; }
}

public class MetricChartPointDto
{
    public DateOnly Date { get; set; }
    public DateTime? RecordedAtUtc { get; set; }
    public decimal Value { get; set; }
    public int Count { get; set; }
    public int AbnormalCount { get; set; }
    public bool IsAbnormal { get; set; }
}

public class MetricTrendDto
{
    public decimal? Average { get; set; }
    public MetricLatestDto? Latest { get; set; }
    public int AbnormalCount { get; set; }
    public IReadOnlyList<MetricChartPointDto> Chart { get; set; } = Array.Empty<MetricChartPointDto>();
}

public class GlucoseTrendDto : MetricTrendDto
{
    public IReadOnlyList<MetricChartPointDto> BeforeMealChart { get; set; } = Array.Empty<MetricChartPointDto>();
    public IReadOnlyList<MetricChartPointDto> AfterMealChart { get; set; } = Array.Empty<MetricChartPointDto>();
}

public class GlucoseRangeDto
{
    public decimal? Lower { get; set; }
    public decimal? Upper { get; set; }
}

public class MetricThresholdsDto
{
    public byte DiabetesType { get; set; }
    public GlucoseRangeDto BeforeMeal { get; set; } = new();
    public GlucoseRangeDto AfterMeal { get; set; } = new();
    public decimal SystolicBpAbnormalFrom { get; set; }
    public decimal DiastolicBpAbnormalFrom { get; set; }
}

public class BloodPressureLatestDto
{
    public int SystolicId { get; set; }
    public int DiastolicId { get; set; }
    public decimal Systolic { get; set; }
    public decimal Diastolic { get; set; }
    public string Unit { get; set; } = "mmHg";
    public DateTime RecordedAtUtc { get; set; }
    public DateOnly RecordedLocalDate { get; set; }
    public bool IsAbnormal { get; set; }
}

public class BloodPressureChartPointDto
{
    public DateOnly Date { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public decimal Systolic { get; set; }
    public decimal Diastolic { get; set; }
    public bool IsAbnormal { get; set; }
}

public class BloodPressureTrendDto
{
    public decimal? AverageSystolic { get; set; }
    public decimal? AverageDiastolic { get; set; }
    public BloodPressureLatestDto? Latest { get; set; }
    public int AbnormalCount { get; set; }
    public IReadOnlyList<BloodPressureChartPointDto> Chart { get; set; } = Array.Empty<BloodPressureChartPointDto>();
}

public class MetricSummaryDto
{
    public int Days { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int TotalAbnormalCount { get; set; }
    public GlucoseTrendDto Glucose { get; set; } = new();
    public MetricTrendDto HbA1c { get; set; } = new();
    public BloodPressureTrendDto BloodPressure { get; set; } = new();
    public MetricThresholdsDto Thresholds { get; set; } = new();
}