using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Dtos;

namespace DiaCompanion.Api.Services;

public interface IReceptionService
{
    Task<ActionResult<OnDutyResponse>> OnDuty(DateOnly? date, byte? shift, string? q);
    Task<ActionResult<List<DoctorShiftDto>>> ListShifts(int? doctorId);
    Task<ActionResult<DoctorShiftDto>> CreateShift(CreateDoctorShiftRequest req);
    Task<ActionResult<List<DoctorShiftDto>>> CreateShiftsBatch(CreateDoctorShiftsBatchRequest req);
    Task<ActionResult<DoctorShiftDto>> SetShiftActive(int id, bool active, string rowVersion);
    Task<IActionResult> DeleteShift(int id, string rowVersion);
}
