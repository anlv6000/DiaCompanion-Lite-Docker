using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Services;

namespace DiaCompanion.Api.Controllers;

/// <summary>UC-22, UC-23, UC-24, UC-26 — ảnh đáy mắt.</summary>
[Route("api/images")]
public class ImagesController : BaseApiController
{
    private readonly IImagesService _service;

    public ImagesController(IImagesService service) => _service = service;


    [HttpGet]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<ActionResult<List<FundusImageDto>>> List(
    [FromQuery] int? patientId, [FromQuery] int? visitId)
    {
        return await _service.List(patientId, visitId);
    }


    /// <summary>UC-22 — nạp ảnh đáy mắt.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.DoctorOnly)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<FundusImageDto>> Upload([FromForm] UploadFundusRequest req)
    {
        return await _service.Upload(req);
    }


    /// <summary>
    /// UC-26 — phục vụ nội dung ảnh.
    ///
    /// QT-18: file nằm ngoài webroot. Mọi lượt xem đều đi qua đây để kiểm quyền,
    /// thay vì phát tĩnh hay dùng presigned URL của dịch vụ đám mây (hệ thống
    /// triển khai tại chỗ, có thể không có internet).
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<ActionResult<FundusImageDto>> Get(int id)
    {
        return await _service.Get(id);
    }


    [HttpGet("{id:int}/content")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<IActionResult> Content(int id)
    {
        return await _service.Content(id);
    }


    /// <summary>
    /// UC-23 — Bác sĩ kiểm duyệt chất lượng ảnh trước khi chạy AI.
    /// </summary>
    [HttpPut("{id:int}/quality")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<IActionResult> SetQuality(int id, QualityCheckRequest req)
    {
        return await _service.SetQuality(id, req);
    }


    /// <summary>UC-24 — thu hồi ảnh (lan sang kết quả AI và review của ảnh đó).</summary>
    [HttpPut("{id:int}/void")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<IActionResult> Void(int id, VoidRequest req)
    {
        return await _service.Void(id, req);
    }
}
