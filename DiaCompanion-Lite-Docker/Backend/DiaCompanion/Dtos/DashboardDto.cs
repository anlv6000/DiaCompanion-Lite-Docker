using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/* ============================ ADMIN (UC-53, UC-58..61) ==================== */

public class DashboardDto
{
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public int? ModelVersionId { get; set; }
    public string Scope { get; set; } = "";
    public int TotalPatients { get; set; }
    public int VisitsThisMonth { get; set; }
    public int PendingTriage { get; set; }
    public int DeferredPending { get; set; }
    public decimal DeferralRate { get; set; }
    public decimal ReferralRate { get; set; }
    public decimal OverrideRate { get; set; }
    public Dictionary<string, int> GradeDistribution { get; set; } = new();
    public string ActiveModel { get; set; } = "";
}
