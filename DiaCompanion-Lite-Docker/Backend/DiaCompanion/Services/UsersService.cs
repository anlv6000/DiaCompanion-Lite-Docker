using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Dtos;

namespace DiaCompanion.Api.Services;

/// <summary>UC-06..11 — quản lý tài khoản Doctor/Receptionist bởi Admin.</summary>
public class UsersService : BaseService, IUsersService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IPasswordHasher _hasher;
    private readonly IClinicClock _clock;

    public UsersService(
        IRepository repository,
        ICurrentUser me,
        IAuditService audit,
        IPasswordHasher hasher,
        IClinicClock clock)
    {
        _repository = repository;
        _me = me;
        _audit = audit;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<ActionResult<PagedResult<StaffUserDto>>> List(
        string? q,
         string? role,
         bool? isActive,
         PageQuery page)
    {

        var data = await _repository.GetStaffPageAsync(q, role, isActive, page);

        return Ok(new PagedResult<StaffUserDto>
        {
            Items = data.Items.Select(MapStaff).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalItems = data.Total
        });
    }

    public async Task<ActionResult<StaffUserDto>> Get(int id)
    {
        var staff = await _repository.GetStaffAsync(id, tracking: false)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy tài khoản nhân viên.");

        return Ok(MapStaff(staff));
    }

    public async Task<ActionResult<TempCredentialResponse>> Create(CreateStaffRequest req)
    {
        // Màn staff chỉ tạo đúng một role: Doctor hoặc Receptionist.
        var staffRole = ResolveRequestedStaffRole(req.Role, req.Roles);
        await EnsureRoleExistsAndActive(staffRole);

        if (staffRole == Roles.Doctor && string.IsNullOrWhiteSpace(req.LicenseNo)
            && string.IsNullOrWhiteSpace(req.FullName)
            && string.IsNullOrWhiteSpace(req.Phone)
            && string.IsNullOrWhiteSpace(req.Email))
            throw AppException.BadRequest(Msg.LicenseRequired, "Bác sĩ phải có số chứng chỉ hành nghề, họ và tên, số điện thoại và email.");

        var email = req.Email.Trim().ToLowerInvariant();
        if (await _repository.EmailExistsAsync(email))
            throw AppException.Conflict(Msg.PhoneTaken, "Email đã được sử dụng cho tài khoản khác.");
        var phone = req.Phone.Trim();
        if (await _repository.PhoneExistsAsync(phone))
            throw AppException.Conflict(Msg.PhoneTaken, "Phone đã được sử dụng cho tài khoản khác.");

        var temp = _hasher.GenerateTempPassword() + "Aa";
        var user = new User
        {
            Phone = phone,
            Email = email,
            PasswordHash = _hasher.Hash(temp),
            FullName = req.FullName.Trim(),
            LicenseNo = staffRole == Roles.Doctor ? req.LicenseNo?.Trim() : null,
            MustChangePassword = true
            // Users.IsActive là cột legacy; trạng thái đăng nhập nằm ở UserRoles.IsActive.
        };

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            _repository.Add(user);
            await _repository.CommitAsync(); // cần User.Id để tạo UserRole

            await _repository.SyncStaffUserRoleAsync(user, staffRole, _me.Id);

            await _audit.LogAsync(
                AuditAction.UserCreate,
                nameof(User),
                user.Id,
                null,
                new { user.Email, Role = staffRole, user.FullName });

            await _repository.CommitAsync();
        });

        return Ok(new TempCredentialResponse
        {
            LoginId = email,
            TempPassword = temp,
            Note = "Mật khẩu tạm chỉ hiển thị một lần. Người dùng phải đổi ở lần đăng nhập đầu.",
            RowVersion = user.ToRowVersion()
        });
    }

    public async Task<IActionResult> Update(int id, UpdateStaffRequest req)
    {
        var staff = await _repository.GetStaffAsync(id, tracking: true)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy tài khoản nhân viên.");

        var user = staff.User;
        _repository.ApplyOriginalRowVersion(user, req.RowVersion);

        var currentRole = SelectStaffRole(staff.Roles)?.Name
            ?? throw AppException.BadRequest(Msg.InvalidData, "Tài khoản không có role nhân viên hợp lệ.");

        // Nếu FE không gửi role thì giữ nguyên role hiện tại.
        var requestedRole = HasRequestedRole(req.Role, req.Roles)
            ? ResolveRequestedStaffRole(req.Role, req.Roles)
            : currentRole;

        var roleChanged = !requestedRole.Equals(currentRole, StringComparison.OrdinalIgnoreCase);
        if (roleChanged)
            await EnsureRoleExistsAndActive(requestedRole);

        if (requestedRole == Roles.Doctor && string.IsNullOrWhiteSpace(req.LicenseNo))
            throw AppException.BadRequest(Msg.LicenseRequired, "Bác sĩ phải có số chứng chỉ hành nghề.");

        var before = new
        {
            user.FullName,
            user.LicenseNo,
            Role = currentRole,
            IsActive = SelectStaffRole(staff.Roles)?.IsActive ?? false
        };

        user.FullName = req.FullName.Trim();
        user.LicenseNo = requestedRole == Roles.Doctor ? req.LicenseNo?.Trim() : null;
        user.UpdatedAt = _clock.UtcNow;

        // Chỉ sync khi thực sự đổi role, tránh cập nhật thông tin làm mở khóa staff ngoài ý muốn.
        if (roleChanged)
            await _repository.SyncStaffUserRoleAsync(user, requestedRole, _me.Id);

        await _audit.LogAsync(
            AuditAction.UserUpdate,
            nameof(User),
            user.Id,
            before,
            new { user.FullName, user.LicenseNo, Role = requestedRole });

        await _repository.CommitAsync();

        return Ok(new
        {
            message = "Cập nhật thông tin thành công.",
            rowVersion = user.ToRowVersion()
        });
    }

    public async Task<IActionResult> SetActive(int id, bool value, ConcurrencyRequest req)
    {
        var staff = await _repository.GetStaffAsync(id, tracking: true)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy tài khoản nhân viên.");

        var user = staff.User;
        _repository.ApplyOriginalRowVersion(user, req.RowVersion);

        if (user.Id == _me.Id && !value)
            throw AppException.BadRequest(
                Msg.Forbidden,
                "Không thể tự khóa quyền nhân viên của tài khoản đang đăng nhập.");

        var selectedRole = SelectStaffRole(staff.Roles)
            ?? throw AppException.BadRequest(Msg.InvalidData, "Tài khoản không có role nhân viên hợp lệ.");

        var oldActive = selectedRole.IsActive;

        // Chỉ thay đổi Doctor/Receptionist trong UserRoles.
        // Patient role của cùng User được giữ nguyên hoàn toàn.
        var changed = await _repository.SetStaffRoleActiveAsync(
            user.Id,
            selectedRole.Name,
            value,
            _me.Id);

        if (!changed)
        {
            throw AppException.BadRequest(
                Msg.InvalidData,
                value
                    ? "Không thể mở khóa vì role nhân viên không còn hoạt động trong bảng Roles."
                    : "Không tìm thấy role nhân viên để khóa.");
        }

        // Chỉ cập nhật timestamp để RowVersion của User đổi cho concurrency.
        // Không dùng và không sửa Users.IsActive.
        user.UpdatedAt = _clock.UtcNow;

        await _audit.LogAsync(
            AuditAction.UserLock,
            nameof(User),
            user.Id,
            new { Role = selectedRole.Name, IsActive = oldActive },
            new { Role = selectedRole.Name, IsActive = value });

        await _repository.CommitAsync();

        return Ok(new
        {
            message = value
                ? "Đã mở khóa quyền nhân viên."
                : "Đã khóa quyền nhân viên.",
            rowVersion = user.ToRowVersion()
        });
    }

    public async Task<ActionResult<TempCredentialResponse>> ResetPassword(int id, ConcurrencyRequest req)
    {
        var staff = await _repository.GetStaffAsync(id, tracking: true)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy tài khoản nhân viên.");

        var user = staff.User;
        _repository.ApplyOriginalRowVersion(user, req.RowVersion);

        var temp = _hasher.GenerateTempPassword() + "Aa";
        user.PasswordHash = _hasher.Hash(temp);
        user.MustChangePassword = true;
        user.UpdatedAt = _clock.UtcNow;

        await _audit.LogAsync(
            AuditAction.PasswordReset,
            nameof(User),
            user.Id,
            detail: "Quản trị viên đặt lại mật khẩu");

        await _repository.CommitAsync();

        return Ok(new TempCredentialResponse
        {
            LoginId = user.Email ?? "",
            TempPassword = temp,
            Note = "Mật khẩu tạm chỉ hiển thị một lần.",
            RowVersion = user.ToRowVersion()
        });
    }

    public async Task<IActionResult> Doctors()
    {
        var doctors = await _repository.GetActiveUsersInRoleAsync(Roles.Doctor);
        return Ok(doctors.Select(u => new { u.Id, u.FullName, u.LicenseNo }).ToList());
    }

    public async Task<IReadOnlyList<LinkableUserDto>> GetLinkableUsersAsync(
        string? keyword,
        CancellationToken ct = default)
    {
        var currentUserId = _me.RequireId();
        return await _repository.GetLinkableUsersForPatientAsync(keyword, currentUserId, ct);
    }

    private async Task EnsureRoleExistsAndActive(string roleName)
    {
        var rows = await _repository.GetActiveRoleNamesByNamesAsync(new[] { roleName });
        if (!rows.Contains(roleName, StringComparer.OrdinalIgnoreCase))
        {
            throw AppException.BadRequest(
                Msg.InvalidData,
                $"Role {roleName} không tồn tại hoặc đang bị khóa trong bảng Roles.");
        }
    }

    private static bool HasRequestedRole(string? role, IEnumerable<string>? roles) =>
        !string.IsNullOrWhiteSpace(role) ||
        (roles?.Any(x => !string.IsNullOrWhiteSpace(x)) ?? false);

    private static string ResolveRequestedStaffRole(string? role, IEnumerable<string>? roles)
    {
        var requested = new List<string>();

        if (!string.IsNullOrWhiteSpace(role))
            requested.Add(role.Trim());

        if (roles is not null)
        {
            requested.AddRange(roles
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()));
        }

        var distinct = requested
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinct.Length != 1)
        {
            throw AppException.BadRequest(
                Msg.RequiredFields,
                "Mỗi tài khoản nhân viên chỉ được chọn một role: Doctor hoặc Receptionist.");
        }

        var selected = distinct[0];
        if (selected.Equals(Roles.Doctor, StringComparison.OrdinalIgnoreCase))
            return Roles.Doctor;
        if (selected.Equals(Roles.Receptionist, StringComparison.OrdinalIgnoreCase))
            return Roles.Receptionist;

        throw AppException.BadRequest(
            Msg.RequiredFields,
            "Vai trò nhân viên chỉ có thể là Doctor hoặc Receptionist.");
    }

    private static StaffRoleData? SelectStaffRole(IEnumerable<StaffRoleData> roles) =>
        roles
            .OrderByDescending(r => r.IsActive)
            .ThenByDescending(r => r.AssignedAt)
            .FirstOrDefault();

    private StaffUserDto MapStaff(StaffUserData staff)
    {
        var user = staff.User;
        var staffRole = SelectStaffRole(staff.Roles);
        var roleName = staffRole?.Name ?? "";

        return new StaffUserDto
        {
            phone = user.Phone,
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = roleName,
            Roles = string.IsNullOrEmpty(roleName) ? new List<string>() : new List<string> { roleName },
            LicenseNo = user.LicenseNo,

            // Trạng thái hiển thị lấy từ UserRoles.IsActive, không phải Users.IsActive.
            IsActive = staffRole?.IsActive ?? false,

            LastLoginAt = _clock.ToLocal(user.LastLoginAt),
            CreatedAt = _clock.ToLocal(user.CreatedAt)!.Value,
            RowVersion = user.ToRowVersion()
        };
    }
}
