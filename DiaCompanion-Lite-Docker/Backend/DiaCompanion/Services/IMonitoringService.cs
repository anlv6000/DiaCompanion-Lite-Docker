using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Services;

public interface IMonitoringService
{
    Task<ActionResult<KeysetResult<HealthMetricDto>>> Metrics(
        int? patientId, MetricType? type, DateOnly? from, DateOnly? to, string? cursor, int size = 50);
    Task<ActionResult<HealthMetricDto>> CreateMetric(CreateMetricRequest req);
    Task<IActionResult> UpdateMetric(int id, CreateMetricRequest req);
    Task<IActionResult> DeleteMetric(int id, ConcurrencyRequest req);
    Task<ActionResult<MetricSummaryDto>> Summary(int patientId, int days = 30);
    Task<ActionResult<List<LifestyleLogDto>>> Lifestyle(int? patientId, int days = 14);
    Task<ActionResult<LifestyleLogDto>> CreateLifestyle(CreateLifestyleRequest req);
    Task<ActionResult<LifestyleLogDto>> UpdateLifestyle(int id, CreateLifestyleRequest req);
    Task<IActionResult> DeleteLifestyle(int id, ConcurrencyRequest req);
    Task<ActionResult<List<MedicationLogDto>>> Today(int? patientId);
    Task<ActionResult<MedicationLogDto>> UpdateMedicationStatus(int id, UpdateMedicationStatusRequest req);
}
