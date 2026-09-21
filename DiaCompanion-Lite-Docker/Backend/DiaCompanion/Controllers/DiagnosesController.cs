using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Services;

namespace DiaCompanion.Api.Controllers;

/// <summary>UC-25, UC-27, UC-28, UC-29 — suy luận AI và diễn tiến.</summary>
[Route("api/diagnoses")]
public class DiagnosesController : BaseApiController
{
    private readonly IDiagnosesService _service;

    public DiagnosesController(IDiagnosesService service) => _service = service;


    /// <summary>
    /// UC-25 + UC-27 + UC-28 — chạy suy luận cho một ảnh.
    ///
    /// Ba việc trong một giao dịch: hai nhánh của mô hình (phân độ + phân vùng),
    /// chỉ số fractal, và tính bất đồng chéo để quyết định có chuyển bác sĩ hay không.
    ///
    /// NT-3: KHÔNG ghi FinalGrade ở đây. Kết quả nằm ở trạng thái "chưa xác nhận"
    /// cho tới khi bác sĩ duyệt hoặc ghi đè (UC-31 / UC-32).
    /// </summary>
    [HttpPost("run/{imageId:int}")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<ActionResult<AiDiagnosisDto>> Run(int imageId, CancellationToken ct)
    {
        return await _service.Run(imageId, ct);
    }


    /// <summary>Chi tiết một kết quả AI.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<AiDiagnosisDto>> Get(int id)
    {
        return await _service.Get(id);
    }


    /// <summary>Các kết quả AI của một ảnh (gồm cả lần chạy lại).</summary>
    [HttpGet("by-image/{imageId:int}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<List<AiDiagnosisDto>>> ByImage(int imageId)
    {
        return await _service.ByImage(imageId);
    }


    /// <summary>Ảnh mask tổn thương của một lần chạy AI.</summary>
    [HttpGet("{id:int}/lesion-mask")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<IActionResult> LesionMask(int id)
    {
        return await _service.LesionMask(id);
    }

    /// <summary>Ảnh mạch máu dùng để tính fractal của một lần chạy AI.</summary>
    [HttpGet("{id:int}/fractal-image")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<IActionResult> FractalImage(int id)
    {
        return await _service.FractalImage(id);
    }


    /// <summary>UC-24 phần kết quả — thu hồi một kết quả AI.</summary>
    [HttpPut("{id:int}/void")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<IActionResult> Void(int id, VoidRequest req)
    {
        return await _service.Void(id, req);
    }


    /// <summary>
    /// UC-29 — diễn tiến: ghép mức DR đã xác nhận, fractal và HbA1c trên một trục
    /// thời gian, nối biến chứng mắt với mức kiểm soát bệnh gốc.
    /// </summary>

    /// <summary>Diễn tiến của chính bệnh nhân đang đăng nhập.</summary>
    [HttpGet("progression/me")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<ActionResult<ProgressionDto>> ProgressionMine([FromQuery] int months = 24)
    {
        return await _service.ProgressionMine(months);
    }

    [HttpGet("progression/{patientId:int}")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<ActionResult<ProgressionDto>> Progression(int patientId, [FromQuery] int months = 24)
    {
        return await _service.Progression(patientId, months);
    }
}
