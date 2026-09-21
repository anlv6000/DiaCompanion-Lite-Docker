using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

public interface IImagesService
{
    Task<ActionResult<List<FundusImageDto>>> List(
        [FromQuery] int? patientId, [FromQuery] int? visitId);
    Task<ActionResult<FundusImageDto>> Get(int id);
    Task<ActionResult<FundusImageDto>> Upload([FromForm] UploadFundusRequest req);
    Task<IActionResult> Content(int id);
    Task<IActionResult> SetQuality(int id, QualityCheckRequest req);
    Task<IActionResult> Void(int id, VoidRequest req);
}
