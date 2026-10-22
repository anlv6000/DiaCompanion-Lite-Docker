using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Services;

namespace DiaCompanion.Api.Controllers;

/// <summary>Chấm mù cho nghiên cứu: vòng 1 (bác sĩ khám), vòng 2/3 (người chấm độc lập, phân xử).</summary>
[Route("api/grading")]
public class GradingController : BaseApiController
{
    private readonly IGradingService _service;

    public GradingController(IGradingService service) => _service = service;

    /// <summary>Bác sĩ khám chấm mù trước khi chạy AI.</summary>
    [HttpPost("round1/{imageId:int}")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public Task<BlindGradeDto> SubmitRound1(int imageId, BlindGradeRequest req, CancellationToken ct)
        => _service.SubmitRound1(imageId, req, ct);

    [HttpGet("round1/{imageId:int}")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<ActionResult<BlindGradeDto?>> GetRound1(int imageId, CancellationToken ct)
        => Ok(await _service.GetRound1(imageId, ct));

    [HttpGet("queue/{round:int}")]
    [Authorize(Roles = Roles.Grader)]
    public Task<GradingQueueDto> Queue(byte round, CancellationToken ct) => _service.Queue(round, ct);

    [HttpGet("images/{imageId:int}/content")]
    [Authorize(Roles = Roles.Grader)]
    public Task<IActionResult> Content(int imageId, CancellationToken ct) => _service.Content(imageId, ct);

    /// <summary>Thông tin lâm sàng hiển thị cạnh ảnh. Bác sĩ khám: đầy đủ (trang AI, chấm mù vòng 1).
    /// Người chấm vòng 2/3 (1.3.1): bản ẩn danh (không mã nghiên cứu, ngày khám, ghi chú), chỉ cho ảnh
    /// thuộc bộ chấm mù, giống điều kiện như bác sĩ vòng 1 có khi chấm.</summary>
    [HttpGet("images/{imageId:int}/context")]
    [Authorize(Roles = "Admin,Doctor,Grader")]
    public Task<ImageClinicalContextDto> Context(int imageId, CancellationToken ct) => _service.ClinicalContext(imageId, ct);

    [HttpPost("round/{round:int}/{imageId:int}")]
    [Authorize(Roles = Roles.Grader)]
    public Task<BlindGradeDto> SubmitGrader(byte round, int imageId, BlindGradeRequest req, CancellationToken ct)
        => _service.SubmitGrader(round, imageId, req, ct);

    [HttpGet("progress")]
    [Authorize(Roles = Roles.GradingProgress)]
    public Task<GradingProgressDto> Progress(CancellationToken ct) => _service.Progress(ct);
}
