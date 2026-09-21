using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;

namespace DiaCompanion.Api.Services;

public interface IAuthService
{
    Task<ActionResult<LoginResponse>> Login(LoginRequest req);
    Task<IActionResult> RequestOtp(RequestOtpRequest req, [FromServices] IWebHostEnvironment env);
    Task<ActionResult<LoginResponse>> LoginOtp(OtpLoginRequest req);
    Task<ActionResult<LoginResponse>> Refresh(RefreshTokenRequest req);
    Task<IActionResult> ForgotPassword(RequestOtpRequest req, [FromServices] IWebHostEnvironment env);
    Task<IActionResult> ResetPassword(ResetPasswordRequest req);
    Task<IActionResult> ChangePassword(ChangePasswordRequest req);
    Task<IActionResult> Logout();
    Task<ActionResult<LoginResponse>> Me();
    Task<ActionResult<StaffProfileDto>> GetProfile();
    Task<ActionResult<StaffProfileDto>> UpdateProfile(UpdateStaffProfileRequest req);
}
