using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

public interface IRecheckService
{
    Task<ActionResult<RecheckDto>> Mine();
    Task<ActionResult<RecheckDto>> ForPatient(int patientId);
    Task<ActionResult<PagedResult<RecheckDto>>> Due(
        [FromQuery] bool overdueOnly = false,
        [FromQuery] int withinDays = 30,
        [FromQuery] PageQuery? page = null);
    Task<IActionResult> OverdueCount();
}
