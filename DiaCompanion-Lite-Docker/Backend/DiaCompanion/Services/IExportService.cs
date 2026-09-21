using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Services;

public interface IExportService
{
    Task<IActionResult> VisitReport(int visitId);
    Task<IActionResult> VisitReportPdf(int visitId);
    Task<ActionResult<object>> DisagreementCases(int? modelVersionId, DateOnly? from, DateOnly? to);
    Task<IActionResult> DisagreementCsv(int? modelVersionId, DateOnly? from, DateOnly? to);

    /// <summary>Bộ dữ liệu nghiên cứu Gap 2 (CSV, đã bỏ định danh) — vai trò Research/Admin.</summary>
    Task<IActionResult> ResearchDatasetCsv(DateOnly? from, DateOnly? to);
}
