using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Services;

namespace DiaCompanion.Api.Controllers;

/// <summary>UC-30..33 — hàng đợi triage và quyết định của bác sĩ.</summary>
[Route("api/triage")]
[Authorize(Roles = Roles.DoctorOrAdmin)]
public class TriageController : BaseApiController
{
    private readonly ITriageService _service;

    public TriageController(ITriageService service) => _service = service;


    /// <summary>
    /// UC-30 — hàng đợi các ca đã có kết quả AI nhưng chưa ai xác nhận.
    ///
    /// Thứ tự ưu tiên: ca bị gắn cờ chuyển bác sĩ lên trước, rồi tới ca cần
    /// chuyển tuyến, rồi theo mức bất đồng giảm dần. Bác sĩ mở màn hình là
    /// thấy ngay ca đáng ngờ nhất.
    ///
    /// Dùng KEYSET pagination chứ không offset: hàng đợi cập nhật liên tục,
    /// mà offset bị trượt cửa sổ khi có bản ghi mới chèn vào giữa lúc lật trang
    /// — bác sĩ có thể BỎ SÓT một ca. Trong worklist lâm sàng đó là lỗi an toàn.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<KeysetResult<TriageItemDto>>> Queue(
    [FromQuery] int? doctorId,
    [FromQuery] bool? deferredOnly,
    [FromQuery] string? q,
    [FromQuery] string? cursor,
    [FromQuery] int size = 25)
    {
        return await _service.Queue(doctorId, deferredOnly, q, cursor, size);
    }


    /// <summary>Số ca đang chờ, để hiện badge trên thanh điều hướng.</summary>
    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        return await _service.Count();
    }


    /// <summary>
    /// UC-31 — bác sĩ phê duyệt kết quả AI.
    /// FinalGrade = phân độ của AI, nhưng chủ thể quyết định vẫn là con người (NT-3).
    /// </summary>
    [HttpPost("{diagnosisId:int}/approve")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<ActionResult<ReviewDto>> Approve(int diagnosisId, ReviewRequest req)
    {
        return await _service.Approve(diagnosisId, req);
    }


    /// <summary>
    /// UC-32 — bác sĩ ghi đè kết quả AI.
    /// BR-04: bắt buộc có lý do. Ca này vào tập dữ liệu người–máy mâu thuẫn (UC-35).
    /// </summary>
    [HttpPost("{diagnosisId:int}/override")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<ActionResult<ReviewDto>> Override(int diagnosisId, OverrideRequest req)
    {
        return await _service.Override(diagnosisId, req);
    }


    /// <summary>
    /// UC-33 — thu hồi review đã lập sai.
    /// Ca tự động quay lại hàng đợi vì unique index chỉ tính review chưa void.
    /// </summary>
    [HttpPut("reviews/{reviewId:int}/void")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<IActionResult> VoidReview(int reviewId, VoidRequest req)
    {
        return await _service.VoidReview(reviewId, req);
    }
}
