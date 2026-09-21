using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Services;
using DiaCompanion.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Controllers;

/// <summary>UC-12..17 — hồ sơ bệnh nhân, kèm cấp tài khoản lúc tạo hồ sơ.</summary>
public class PatientsController : BaseApiController
{
    private readonly IPatientsService _service;

    public PatientsController(IPatientsService service) => _service = service;


    /// <summary>
    /// UC-12 — tìm kiếm và lọc, phân trang offset (QT-14).
    /// Tìm bỏ dấu: "nguyen van an" khớp "Nguyễn Văn Ấn" (QT-15).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Roles.FrontDesk)]
    public async Task<ActionResult<PagedResult<PatientListItemDto>>> Search(
    [FromQuery] string? q,
    [FromQuery] byte? diabetesType,
    [FromQuery] byte? grade,
    [FromQuery] PageQuery paging)
    {
        return await _service.Search(q, diabetesType, grade, paging);
    }


    /// <summary>UC-13 — chi tiết hồ sơ.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.FrontDesk)]
    public async Task<ActionResult<PatientDetailDto>> Get(int id)
    {
        return await _service.Get(id);
    }


    /// <summary>UC-16 — bệnh nhân xem hồ sơ của chính mình.</summary>
    [HttpGet("me")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<ActionResult<PatientDetailDto>> GetMine()
    {
        return await _service.GetMine();
    }


    /// <summary>
    /// UC-14 — tạo hồ sơ VÀ cấp tài khoản trong cùng một thao tác.
    ///
    /// Thay cho luồng cũ (bệnh nhân tự đăng ký rồi nhập mã liên kết): phòng khám
    /// tạo cả hai cùng lúc nên tài khoản gắn hồ sơ ngay từ đầu, loại bỏ hoàn toàn
    /// bài toán liên kết nhầm hồ sơ.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Receptionist)]
    public async Task<ActionResult<CreatePatientResponse>> Create(CreatePatientRequest req)
    {
        return await _service.Create(req);
    }


    /// <summary>UC-15 — cập nhật hồ sơ.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.StaffPatient)]
    public async Task<ActionResult<PatientDetailDto>> Update(int id, UpdatePatientRequest req)
    {
        return await _service.Update(id, req);
    }


    /// <summary>UC-17 — bệnh nhân tự cập nhật thông tin liên hệ.</summary>
    [HttpPut("me")]
    [Authorize(Roles = Roles.Patient)]
    public async Task<IActionResult> UpdateMine(UpdateMyProfileRequest req)
    {
        return await _service.UpdateMine(req);
    }



/// <summary>
/// Gửi OTP tới số điện thoại mới. Số mới phải chưa được dùng bởi hồ sơ hoặc
/// tài khoản đang hoạt động khác.
/// </summary>
[HttpPost("me/phone/request-otp")]
[Authorize(Roles = Roles.Patient)]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("otp-request-limit")]
public async Task<IActionResult> RequestPhoneChangeOtp(
    RequestPhoneChangeOtpRequest req,
    [FromServices] IWebHostEnvironment env)
{
    return await _service.RequestPhoneChangeOtp(req, env);
}

/// <summary>
/// Xác minh OTP rồi mới đổi số điện thoại hồ sơ và tài khoản đăng nhập.
/// RowVersion bảo vệ khỏi lost update.
/// </summary>
[HttpPost("me/phone/confirm")]
[Authorize(Roles = Roles.Patient)]
public async Task<IActionResult> ConfirmPhoneChange(ConfirmPhoneChangeRequest req)
{
    return await _service.ConfirmPhoneChange(req);
}


    /// <summary>
    /// Cấp lại mật khẩu tạm tại quầy — thay cho luồng liên kết tài khoản cũ.
    /// Dùng khi bệnh nhân quên mật khẩu và không nhận được OTP.
    /// </summary>
    [HttpPost("{id:int}/reissue-credentials")]
    [Authorize(Roles = Roles.AllRole)]
    public async Task<ActionResult<TempCredentialResponse>> ReissueCredentials(int id)
    {
        return await _service.ReissueCredentials(id);
    }

    /// <summary>
    /// Thu hồi hồ sơ nhập nhầm hoặc trùng.
    /// Nhờ filtered unique index, số điện thoại được giải phóng để đăng ký lại.
    /// </summary>
    [HttpPut("{id:int}/void")]
    [Authorize(Roles = Roles.Receptionist)]
    public async Task<IActionResult> Void(int id, VoidRequest req)
    {
        return await _service.Void(id, req);
    }



    /// <summary>
    /// Admin — danh sách bệnh nhân để quản lý tài khoản.
    /// Trạng thái lấy từ UserRoles.Patient.IsActive.
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PagedResult<AdminPatientDto>>> AdminList(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] PageQuery paging)
    {
        return await _service.AdminList(
            q,
            status,
            paging);
    }


    /// <summary>
    /// Admin — chỉ sửa họ tên, giới tính, địa chỉ.
    /// </summary>
    [HttpPut("admin/{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AdminUpdate(
        int id,
        AdminUpdatePatientRequest req)
    {
        return await _service.AdminUpdate(
            id,
            req);
    }


    /// <summary>
    /// Admin — khóa/mở riêng role Patient.
    /// Không ảnh hưởng role Doctor/Receptionist nếu cùng User.
    /// </summary>
    [HttpPut("admin/{id:int}/active")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> SetPatientAccountActive(
        int id,
        ConcurrencyRequest req,
        [FromQuery] bool value)
    {
        return await _service.SetPatientAccountActive(
            id,
            value,
            req);
    }
}
