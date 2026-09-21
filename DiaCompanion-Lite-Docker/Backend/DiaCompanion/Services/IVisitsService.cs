using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

public interface IVisitsService
{
    Task<ActionResult<PagedResult<VisitDto>>> List(
        int? patientId, int? doctorId, DateOnly? from, DateOnly? to,
        byte? status, PageQuery page);
    Task<ActionResult<VisitDto>> Get(int id);
    Task<ActionResult<VisitDto>> Create(CreateVisitRequest req);
    Task<ActionResult<VisitDto>> Close(int id, CloseVisitRequest req);
    Task<IActionResult> Void(int id, VoidRequest req);
    Task<ActionResult<VisitHealthMetricsDto>> GetHealthMetrics(int visitId);
    Task<ActionResult<VisitHealthMetricsDto>> SaveHealthMetrics(int visitId, SaveVisitHealthMetricsRequest req);

    Task<PagedResult<VisitDto>> GetMineAsync(int userId,PageQuery page);

    Task<VisitDto> GetMineByIdAsync(int userId,int visitId);
}
