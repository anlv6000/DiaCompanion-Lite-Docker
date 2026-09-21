using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DiaCompanion.Api.Controllers;

/// <summary>UC-01, UC-04, UC-05.</summary>
public class AuthController : BaseApiController
{
    private readonly IAuthService _service;

    public AuthController(IAuthService service) => _service = service;


    /// <summary>
    /// UC-01 — đăng nhập bằng mật khẩu.
    /// Nhân viên dùng email; bệnh nhân dùng SỐ ĐIỆN THOẠI (LI-6).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("login-limit")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        return await _service.Login(req);
    }


    /// <summary>
    /// UC-01 phương thức 2 — bệnh nhân yêu cầu mã OTP.
    ///
    /// PHẠM VI v1.0: hệ thống KHÔNG gửi SMS (LI-5). Mã được trả về cho nhân viên
    /// quầy tiếp đón đọc cho bệnh nhân, hoặc hiển thị trong môi trường Development.
    /// Khi có cổng SMS thì chỉ cần cắm ISmsSender vào OtpService.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("request-otp")]
    [EnableRateLimiting("otp-request-limit")]
    public async Task<IActionResult> RequestOtp(RequestOtpRequest req, [FromServices] IWebHostEnvironment env)
    {
        return await _service.RequestOtp(req, env);
    }


    /// <summary>UC-01 phương thức 2 — đăng nhập bằng OTP.</summary>
    [AllowAnonymous]
    [HttpPost("login-otp")]
    public async Task<ActionResult<LoginResponse>> LoginOtp(OtpLoginRequest req)
    {
        return await _service.LoginOtp(req);
    }


    /// <summary>Cấp lại access token. Role luôn được đọc lại từ Roles/UserRoles đang active.</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshTokenRequest req)
    {
        return await _service.Refresh(req);
    }


    /// <summary>UC-04 — quên mật khẩu, đặt lại bằng OTP.</summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(RequestOtpRequest req, [FromServices] IWebHostEnvironment env)
    {
        return await _service.ForgotPassword(req, env);
    }


    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
    {
        return await _service.ResetPassword(req);
    }


    /// <summary>UC-05 — đổi mật khẩu khi đã đăng nhập.</summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        return await _service.ChangePassword(req);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        return await _service.Logout();
    }


    [HttpGet("me")]
    public async Task<ActionResult<LoginResponse>> Me()
    {
        return await _service.Me();
    }


    /// <summary>Hồ sơ cá nhân của Doctor/Receptionist đang đăng nhập.</summary>
    [HttpGet("profile")]
    [Authorize(Roles = Roles.DoctorOrReception)]
    public async Task<ActionResult<StaffProfileDto>> Profile()
    {
        return await _service.GetProfile();
    }


    /// <summary>Doctor/Receptionist tự cập nhật thông tin hồ sơ được phép sửa.</summary>
    [HttpPut("profile")]
    [Authorize(Roles = Roles.DoctorOrReception)]
    public async Task<ActionResult<StaffProfileDto>> UpdateProfile(UpdateStaffProfileRequest req)
    {
        return await _service.UpdateProfile(req);
    }
}
