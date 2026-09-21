using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

/// <summary>UC-01, UC-04, UC-05.</summary>
public class AuthService : BaseService, IAuthService
{
    private readonly IRepository _repository;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IOtpService _otp;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _me;
    private readonly IClinicClock _clock;

    public AuthService(
        IRepository repository,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IOtpService otp,
        IAuditService audit,
        ICurrentUser me,
        IClinicClock clock)
    {
        _repository = repository;
        _hasher = hasher;
        _jwt = jwt;
        _otp = otp;
        _audit = audit;
        _me = me;
        _clock = clock;
    }

    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) && string.IsNullOrWhiteSpace(req.Phone))
            throw AppException.BadRequest(Msg.RequiredFields, "Vui lòng nhập email hoặc số điện thoại.");

        var auth = await _repository.GetUserByLoginAsync(req.Email, req.Phone);
        var user = auth?.User;

        if (user is null || !_hasher.Verify(req.Password, user.PasswordHash))
        {
            await _audit.LogAsync(AuditAction.LoginFailed, "User", user?.Id,
                detail: $"Đăng nhập thất bại: {req.Email ?? req.Phone}");
            await _repository.CommitAsync();
            throw AppException.Unauthorized(Msg.BadCredentials, "Tài khoản đăng nhập hoặc mật khẩu không đúng.");
        }

        EnsureAccountCanAuthenticate(auth!);
        return await IssueTokenAsync(auth!);
    }

    public async Task<IActionResult> RequestOtp(RequestOtpRequest req, [FromServices] IWebHostEnvironment env)
    {
        var auth = await _repository.GetUserByPhoneAsync(req.Phone);

        // Không để lộ số điện thoại nào đã đăng ký. OTP đăng nhập chỉ áp dụng
        // cho tài khoản có role Patient đang active trong DB.
        if (auth is null || !auth.Roles.Contains(Roles.Patient, StringComparer.OrdinalIgnoreCase))
            return Ok(new { message = "Nếu số điện thoại đã đăng ký, mã xác minh sẽ được cấp." });

        var code = await _otp.IssueAsync(req.Phone, OtpPurpose.Login, issuedBy: null);
        await _audit.LogAsync(AuditAction.OtpIssued, "User", auth.User.Id, detail: "Cấp OTP đăng nhập");
        await _repository.CommitAsync();

        return Ok(new
        {
            message = "Nếu số điện thoại đã đăng ký, mã xác minh sẽ được cấp.",
            devCode = env.IsDevelopment() ? code : null,
            note = env.IsDevelopment()
                ? "Chỉ hiển thị ở môi trường Development. Bản triển khai thật cần cổng SMS."
                : null
        });
    }

    public async Task<ActionResult<LoginResponse>> LoginOtp(OtpLoginRequest req)
    {
        var auth = await _repository.GetUserByPhoneAsync(req.Phone);
        if (auth is null || !auth.Roles.Contains(Roles.Patient, StringComparer.OrdinalIgnoreCase))
            throw AppException.Unauthorized(Msg.BadCredentials, "Số điện thoại hoặc mã xác minh không đúng.");

        if (!await _otp.VerifyAsync(req.Phone, req.Code, OtpPurpose.Login))
            throw AppException.Unauthorized(Msg.OtpInvalid, "Mã xác minh không đúng hoặc đã hết hạn.");

        EnsureAccountCanAuthenticate(auth);
        return await IssueTokenAsync(auth);
    }

    public async Task<ActionResult<LoginResponse>> Refresh(RefreshTokenRequest req)
    {
        var userId = _jwt.ValidateRefreshToken(req.RefreshToken)
            ?? throw AppException.Unauthorized(Msg.SessionExpired, "Refresh token không hợp lệ hoặc đã hết hạn.");

        // Quan trọng: không dùng role nằm trong token cũ. Mỗi lần refresh đều
        // đọc lại User + Roles/UserRoles đang active từ database.
        var auth = await _repository.GetAuthUserByIdAsync(userId)
            ?? throw AppException.Unauthorized(Msg.SessionExpired, "Tài khoản không còn tồn tại.");

        EnsureAccountCanAuthenticate(auth);
        return await IssueTokenAsync(auth, updateLastLogin: false);
    }

    public async Task<IActionResult> ForgotPassword(RequestOtpRequest req, [FromServices] IWebHostEnvironment env)
    {
        var auth = await _repository.GetUserByPhoneAsync(req.Phone);
        if (auth is null || auth.Roles.Count == 0)
            return Ok(new { message = "Nếu số điện thoại đã đăng ký, mã xác minh sẽ được cấp." });

        var code = await _otp.IssueAsync(req.Phone, OtpPurpose.ResetPassword, issuedBy: null);
        await _repository.CommitAsync();
        return Ok(new { message = "Đã cấp mã xác minh.", devCode = env.IsDevelopment() ? code : null });
    }

    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
    {
        _hasher.EnsureStrong(req.NewPassword);

        var auth = await _repository.GetUserByPhoneAsync(req.Phone)
            ?? throw AppException.BadRequest(Msg.OtpInvalid, "Mã xác minh không đúng hoặc đã hết hạn.");

        EnsureAccountCanAuthenticate(auth);

        if (!await _otp.VerifyAsync(req.Phone, req.Code, OtpPurpose.ResetPassword))
            throw AppException.BadRequest(Msg.OtpInvalid, "Mã xác minh không đúng hoặc đã hết hạn.");

        var user = auth.User;
        user.PasswordHash = _hasher.Hash(req.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = _clock.UtcNow;

        await _audit.LogAsync(AuditAction.PasswordReset, "User", user.Id, detail: "Đặt lại mật khẩu qua OTP");
        await _repository.CommitAsync();
        return Ok(new { message = "Đặt lại mật khẩu thành công." });
    }

    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        _hasher.EnsureStrong(req.NewPassword);

        var id = _me.RequireId();
        var auth = await _repository.GetAuthUserByIdAsync(id)
            ?? throw AppException.Unauthorized(Msg.SessionExpired, "Phiên đăng nhập đã hết hạn.");
        EnsureAccountCanAuthenticate(auth);

        var user = auth.User;
        var wasTemporaryPassword = user.MustChangePassword;
        if (!wasTemporaryPassword)
        {
            if (string.IsNullOrWhiteSpace(req.CurrentPassword))
                throw AppException.BadRequest(Msg.RequiredFields, "Vui lòng nhập mật khẩu hiện tại.");
            if (!_hasher.Verify(req.CurrentPassword, user.PasswordHash))
                throw AppException.BadRequest(Msg.BadCredentials, "Mật khẩu hiện tại không đúng.");
        }

        user.PasswordHash = _hasher.Hash(req.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = _clock.UtcNow;

        await _audit.LogAsync(
            AuditAction.PasswordChange,
            "User",
            user.Id,
            detail: wasTemporaryPassword ? "Đổi mật khẩu tạm lần đầu" : "Đổi mật khẩu chủ động");
        await _repository.CommitAsync();

        // Token đăng nhập cũ vẫn chứa claim mustChangePassword=true. Nếu tiếp tục
        // dùng token đó, MustChangePasswordMiddleware sẽ chặn các API khác dù DB
        // đã đổi mật khẩu xong. Cấp token mới ngay để web/mobile hoàn tất first-login.
        var token = _jwt.CreateAccessToken(user, auth.Roles, auth.PatientId, out var expiresAt);
        var refreshToken = _jwt.CreateRefreshToken(user.Id, out var refreshExpiresAt);

        return Ok(new
        {
            message = "Đổi mật khẩu thành công.",
            token,
            expiresAt = _clock.ToLocal(expiresAt)!.Value,
            refreshToken,
            refreshTokenExpiresAt = _clock.ToLocal(refreshExpiresAt)!.Value,
            userId = user.Id,
            fullName = user.FullName,
            role = Roles.Primary(auth.Roles),
            roles = auth.Roles.ToList(),
            patientId = auth.PatientId,
            mustChangePassword = false,
            defaultRoute = Roles.DefaultRoute(auth.Roles)
        });
    }

    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync(AuditAction.Logout, "User", _me.Id);
        await _repository.CommitAsync();
        return Ok(new { message = "Đã đăng xuất." });
    }

    public async Task<ActionResult<LoginResponse>> Me()
    {
        var auth = await _repository.GetAuthUserByIdAsync(_me.RequireId())
            ?? throw AppException.Unauthorized(Msg.SessionExpired, "Phiên đăng nhập đã hết hạn.");
        EnsureAccountCanAuthenticate(auth);
        return Ok(ToResponse(auth, token: "", expiresAt: default, refreshToken: "", refreshExpiresAt: default));
    }

    public async Task<ActionResult<StaffProfileDto>> GetProfile()
    {
        var auth = await _repository.GetAuthUserByIdAsync(_me.RequireId())
            ?? throw AppException.Unauthorized(Msg.SessionExpired, "Phiên đăng nhập đã hết hạn.");

        EnsureAccountCanAuthenticate(auth);
        var staffRole = ResolveProfileStaffRole(auth.Roles);
        return Ok(ToStaffProfile(auth.User, staffRole));
    }

    public async Task<ActionResult<StaffProfileDto>> UpdateProfile(UpdateStaffProfileRequest req)
    {
        var auth = await _repository.GetAuthUserByIdAsync(_me.RequireId())
            ?? throw AppException.Unauthorized(Msg.SessionExpired, "Phiên đăng nhập đã hết hạn.");

        EnsureAccountCanAuthenticate(auth);
        var staffRole = ResolveProfileStaffRole(auth.Roles);
        var user = auth.User;
        _repository.ApplyOriginalRowVersion(user, req.RowVersion);

        var fullName = req.FullName.Trim();
        var phone = req.Phone.Trim();
        var licenseNo = string.IsNullOrWhiteSpace(req.LicenseNo) ? null : req.LicenseNo.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
            throw AppException.BadRequest(Msg.RequiredFields, "Họ tên không được để trống.");

        if (staffRole == Roles.Doctor && string.IsNullOrWhiteSpace(licenseNo))
            throw AppException.BadRequest(Msg.LicenseRequired, "Bác sĩ phải có số chứng chỉ hành nghề.");

        if (await _repository.PhoneExistsAsync(phone, user.Id))
            throw AppException.Conflict(Msg.PhoneTaken, "Số điện thoại đã được sử dụng cho tài khoản khác.");

        var before = new
        {
            user.FullName,
            user.Phone,
            user.LicenseNo
        };

        user.FullName = fullName;
        user.Phone = phone;
        user.LicenseNo = staffRole == Roles.Doctor ? licenseNo : null;
        user.UpdatedAt = _clock.UtcNow;

        await _audit.LogAsync(
            AuditAction.UserUpdate,
            nameof(User),
            user.Id,
            before,
            new { user.FullName, user.Phone, user.LicenseNo },
            detail: "Người dùng tự cập nhật hồ sơ cá nhân");

        await _repository.CommitAsync();
        return Ok(ToStaffProfile(user, staffRole));
    }

    private async Task<ActionResult<LoginResponse>> IssueTokenAsync(AuthUserData auth, bool updateLastLogin = true)
    {
        var token = _jwt.CreateAccessToken(auth.User, auth.Roles, auth.PatientId, out var expiresAt);
        var refreshToken = _jwt.CreateRefreshToken(auth.User.Id, out var refreshExpiresAt);

        if (updateLastLogin)
        {
            auth.User.LastLoginAt = _clock.UtcNow;
            await _audit.LogAsync(AuditAction.Login, "User", auth.User.Id);
            await _repository.CommitAsync();
        }

        return Ok(ToResponse(auth, token, expiresAt, refreshToken, refreshExpiresAt));
    }

    private LoginResponse ToResponse(
        AuthUserData auth,
        string token,
        DateTime expiresAt,
        string refreshToken,
        DateTime refreshExpiresAt) => new()
    {
        Token = token,
        ExpiresAt = expiresAt == default ? default : _clock.ToLocal(expiresAt)!.Value,
        RefreshToken = refreshToken,
        RefreshTokenExpiresAt = refreshExpiresAt == default ? default : _clock.ToLocal(refreshExpiresAt)!.Value,
        UserId = auth.User.Id,
        FullName = auth.User.FullName,
        Role = Roles.Primary(auth.Roles),
        Roles = auth.Roles.ToList(),
        PatientId = auth.PatientId,
        MustChangePassword = auth.User.MustChangePassword,
        DefaultRoute = Roles.DefaultRoute(auth.Roles)
    };

    private static string ResolveProfileStaffRole(IEnumerable<string> roles)
    {
        var set = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
        if (set.Contains(Roles.Doctor)) return Roles.Doctor;
        if (set.Contains(Roles.Receptionist)) return Roles.Receptionist;

        throw AppException.Forbidden(
            Msg.Forbidden,
            "Trang hồ sơ nhân viên chỉ dành cho Bác sĩ hoặc Lễ tân.");
    }

    private StaffProfileDto ToStaffProfile(DiaCompanion.Api.Entities.User user, string role) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        Phone = user.Phone,
        Role = role,
        LicenseNo = role == Roles.Doctor ? user.LicenseNo : null,
        LastLoginAt = _clock.ToLocal(user.LastLoginAt),
        CreatedAt = _clock.ToLocal(user.CreatedAt)!.Value,
        RowVersion = user.ToRowVersion()
    };

    private static void EnsureAccountCanAuthenticate(AuthUserData auth)
    {
        // Trạng thái đăng nhập được quyết định bởi UserRoles.IsActive + Roles.IsActive.
        if (auth.Roles.Count == 0)
            throw AppException.Unauthorized(
                Msg.Forbidden,
                "Tài khoản hiện không có vai trò đang hoạt động.");
    }
}
