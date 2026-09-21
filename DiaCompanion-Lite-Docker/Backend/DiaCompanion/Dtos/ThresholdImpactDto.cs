using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/// <summary>UC-63: cho admin thấy hệ quả TRƯỚC khi đổi ngưỡng.</summary>
public class ThresholdImpactDto
{
    public decimal CurrentThreshold { get; set; }
    public decimal ProposedThreshold { get; set; }
    public int TotalCases { get; set; }
    public int CurrentDeferred { get; set; }
    public int ProjectedDeferred { get; set; }
    public decimal CurrentRate { get; set; }
    public decimal ProjectedRate { get; set; }
    public string Note { get; set; } = "";
}
