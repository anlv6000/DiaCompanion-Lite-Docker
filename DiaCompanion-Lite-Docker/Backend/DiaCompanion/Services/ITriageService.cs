using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

public interface ITriageService
{
    Task<ActionResult<KeysetResult<TriageItemDto>>> Queue(
        [FromQuery] int? doctorId,
        [FromQuery] bool? deferredOnly,
        [FromQuery] string? q,
        [FromQuery] string? cursor,
        [FromQuery] int size = 25);
    Task<IActionResult> Count();
    Task<ActionResult<ReviewDto>> Approve(int diagnosisId, ReviewRequest req);
    Task<ActionResult<ReviewDto>> Override(int diagnosisId, OverrideRequest req);
    Task<IActionResult> VoidReview(int reviewId, VoidRequest req);
}
