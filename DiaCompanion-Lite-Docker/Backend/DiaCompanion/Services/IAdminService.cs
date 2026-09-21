using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

public interface IAdminService
{
    Task<ActionResult<DashboardDto>> Dashboard(DateOnly? from, DateOnly? to, int? modelVersionId);
    Task<ActionResult<List<SystemConfigDto>>> Configs();
    Task<IActionResult> UpdateConfig(string key, UpdateConfigRequest req);
    Task<ActionResult<ThresholdImpactDto>> ThresholdImpact(
        [FromQuery] string key, [FromQuery] decimal proposed);
    Task<ActionResult<List<ModelVersionDto>>> Models();
    Task<ActionResult<ModelVersionDto>> RegisterModel(RegisterModelRequest req);
    Task<IActionResult> ActivateModel(int id, ConcurrencyRequest req);
    Task<IActionResult> DeleteModel(int id, string rowVersion);
    Task<ActionResult<KeysetResult<AuditLogDto>>> Audit(
        [FromQuery] string? action, [FromQuery] string? entityType, [FromQuery] int? entityId,
        [FromQuery] int? userId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? cursor, [FromQuery] int size = 25);
}
