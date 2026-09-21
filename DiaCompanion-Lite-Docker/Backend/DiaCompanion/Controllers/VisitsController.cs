using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Services;

namespace DiaCompanion.Api.Controllers;

/// <summary>UC-18..21 — lượt khám.</summary>
public class VisitsController : BaseApiController
{
    private readonly IVisitsService _service;
    private readonly CurrentUser _me;
    private readonly IVisitMaintenanceService _maintenance;
    public VisitsController(IVisitsService service, CurrentUser me, IVisitMaintenanceService maintenance)
    {
        _service = service;
        _me = me;
        _maintenance = maintenance;
    }


    /// <summary>
    /// Danh sách lượt khám dùng cho danh sách toàn quầy và tab lịch sử của hồ sơ bệnh nhân.
    /// Không tự giới hạn theo bác sĩ đang đăng nhập; bộ lọc doctorId chỉ áp dụng khi client gửi.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Roles.StaffPatient)]
    public async Task<ActionResult<PagedResult<VisitDto>>> List(
        [FromQuery] int? patientId, [FromQuery] int? doctorId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] byte? status, [FromQuery] PageQuery paging)
    {
        return await _service.List(patientId, doctorId, from, to, status, paging);
    }


    /// <summary>
    /// Danh sách lượt khám được giao cho chính bác sĩ đang đăng nhập.
    /// Endpoint riêng để không làm ảnh hưởng danh sách toàn quầy và lịch sử đầy đủ của bệnh nhân.
    /// </summary>
    [HttpGet("assigned-to-me")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<ActionResult<PagedResult<VisitDto>>> AssignedToMe(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] byte? status, [FromQuery] PageQuery paging)
    {
        var doctorId = _me.RequireId();
        return await _service.List(null, doctorId, from, to, status, paging);
    }


    /// <summary>UC-19 — chi tiết lượt khám kèm ảnh, kết quả AI và review.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<ActionResult<VisitDto>> Get(int id)
    {
        return await _service.Get(id);
    }


    /// <summary>UC-18 — tạo lượt khám.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Receptionist)]
    public async Task<ActionResult<VisitDto>> Create(CreateVisitRequest req)
    {
        return await _service.Create(req);
    }


    /// <summary>Lấy các chỉ số sức khỏe được bác sĩ ghi trong lượt khám.</summary>
    [HttpGet("{id:int}/health-metrics")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<ActionResult<VisitHealthMetricsDto>> HealthMetrics(int id)
    {
        return await _service.GetHealthMetrics(id);
    }

    /// <summary>Ghi/cập nhật toàn bộ form chỉ số sức khỏe của lượt khám đang mở.</summary>
    [HttpPut("{id:int}/health-metrics")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<ActionResult<VisitHealthMetricsDto>> SaveHealthMetrics(
        int id, SaveVisitHealthMetricsRequest req)
    {
        return await _service.SaveHealthMetrics(id, req);
    }


    /// <summary>
    /// UC-20 — nhập kết luận và đóng lượt khám.
    /// BR-12: bắt buộc có kết luận. BR-19: chu kỳ tái khám suy từ mức DR đã xác nhận.
    /// </summary>
    [HttpPut("{id:int}/close")]
    [Authorize(Roles = Roles.DoctorOnly)]
    public async Task<ActionResult<VisitDto>> Close(int id, CloseVisitRequest req)
    {
        return await _service.Close(id, req);
    }


    /// <summary>UC-21 — thu hồi lượt khám (lan sang ảnh, kết quả AI, review, đơn thuốc).</summary>
    [HttpPut("{id:int}/void")]
    [Authorize(Roles = Roles.DoctorOrReception)]
    public async Task<IActionResult> Void(int id, VoidRequest req)
    {
        return await _service.Void(id, req);
    }


    /// <summary>Admin chạy thủ công để test xử lý lượt khám cuối ngày.</summary>
    [HttpPost("maintenance/process-open")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ProcessOpenVisits([FromQuery] bool includeToday = true, CancellationToken ct = default)
    {
        var result = await _maintenance.ProcessAsync(includeToday, ct);
        return Ok(new
        {
            checkedCount = result.Checked,
            autoClosed = result.AutoClosed,
            carriedForward = result.CarriedForward
        });
    }

    [HttpGet("me")] 
    [Authorize(Roles = Roles.Patient)]
    public async Task<ActionResult<PagedResult<VisitDto>>> Mine(
        [FromQuery] PageQuery paging)
    {
        var userId = _me.RequireId();

        var result = await _service.GetMineAsync(
            userId,
            paging);

        return Ok(result);
    }

    [HttpGet("me/{id:int}")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<ActionResult<VisitDto>> MineById(int id)
    {
        var userId = _me.RequireId();

        var result = await _service.GetMineByIdAsync(
            userId,
            id);

        return Ok(result);
    }


}
