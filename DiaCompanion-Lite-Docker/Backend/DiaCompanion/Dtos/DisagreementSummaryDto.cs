using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class DisagreementSummaryDto
{
    public int TotalReviewed { get; set; }
    public int TotalOverridden { get; set; }
    public decimal OverrideRate { get; set; }
    public int DeferredCount { get; set; }
    public decimal OverrideRateWithinDeferred { get; set; }
    public decimal OverrideRateOutsideDeferred { get; set; }
    public decimal AvgDisagreement { get; set; }
    /// <summary>
    /// Chỉ số đáng quan tâm nhất: nếu tỉ lệ ghi đè trong nhóm bị gắn cờ CAO HƠN
    /// hẳn nhóm không gắn cờ thì cơ chế deferral đang bắt đúng ca khó.
    /// </summary>
    public string Interpretation { get; set; } = "";
}
