using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    // ============================================================
    // DANH SÁCH STAFF
    // ============================================================
    public async Task<StaffPage> GetStaffPageAsync(
        string? q,
        string? roleName,
        bool? isActive,
        PageQuery page,
        CancellationToken ct = default)
    {
        // Chỉ lấy user từng có Doctor/Receptionist; không dùng Users.IsActive.
        // Admin và user chỉ có Patient không thuộc màn quản lý staff này.
        var query = _db.Users
            .AsNoTracking()
            .Where(u => u.UserRoles.Any(ur =>
                ur.Role.Name == Roles.Doctor ||
                ur.Role.Name == Roles.Receptionist))
            .Where(u => !u.UserRoles.Any(ur =>
                ur.Role.Name == Roles.Admin &&
                ur.IsActive &&
                ur.Role.IsActive));

        // ========================================================
        // FILTER
        // ========================================================
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u =>
                u.FullName.Contains(term) ||
                (u.Email != null && u.Email.Contains(term)));
        }

        var staffRole = NormalizeManagedStaffRole(roleName);
        if (!string.IsNullOrWhiteSpace(roleName) && staffRole is null)
        {
            // Role ngoài Doctor/Receptionist không thuộc danh sách này.
            query = query.Where(_ => false);
        }
        else if (staffRole is not null)
        {
            query = query.Where(u =>
                u.UserRoles.Any(ur => ur.Role.Name == staffRole));
        }

        // Trạng thái tài khoản staff lấy hoàn toàn từ UserRoles.IsActive.
        // null = tất cả; true = đang mở; false = đã khóa.
        if (isActive is bool active)
        {
            if (staffRole is not null)
            {
                query = query.Where(u => u.UserRoles.Any(ur =>
                    ur.Role.Name == staffRole &&
                    ur.IsActive == active));
            }
            else if (active)
            {
                query = query.Where(u => u.UserRoles.Any(ur =>
                    (ur.Role.Name == Roles.Doctor || ur.Role.Name == Roles.Receptionist) &&
                    ur.IsActive));
            }
            else
            {
                query = query.Where(u => !u.UserRoles.Any(ur =>
                    (ur.Role.Name == Roles.Doctor || ur.Role.Name == Roles.Receptionist) &&
                    ur.IsActive));
            }
        }

        // ========================================================
        // COUNT + SORT + PHÂN TRANG
        // ========================================================
        var total = await query.CountAsync(ct);

        query = page.Sort?.ToLowerInvariant() switch
        {
            "name" => page.Desc
                ? query.OrderByDescending(u => u.FullName)
                : query.OrderBy(u => u.FullName),
            "created" => page.Desc
                ? query.OrderByDescending(u => u.CreatedAt)
                : query.OrderBy(u => u.CreatedAt),
            _ => query.OrderBy(u => u.FullName)
        };

        var users = await query
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(ct);

        // ========================================================
        // LẤY STAFF ROLE CỦA CÁC USER TRONG TRANG
        // ========================================================
        var userIds = users.Select(u => u.Id).ToArray();

        var roleDataRows = await _db.UserRoles
            .AsNoTracking()
            .Where(ur =>
                userIds.Contains(ur.UserId) &&
                (ur.Role.Name == Roles.Doctor || ur.Role.Name == Roles.Receptionist))
            .Select(ur => new
            {
                ur.UserId,
                RoleName = ur.Role.Name,
                AssignmentIsActive = ur.IsActive,
                RoleIsActive = ur.Role.IsActive,
                ur.AssignedAt
            })
            .ToListAsync(ct);

        var rolesByUser = roleDataRows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<StaffRoleData>)g
                    .Select(x => new StaffRoleData(
                        x.RoleName,
                        x.AssignmentIsActive,
                        x.RoleIsActive,
                        x.AssignedAt))
                    .OrderByDescending(r => r.IsActive)
                    .ThenByDescending(r => r.AssignedAt)
                    .ToList());

        var items = users
            .Select(u => new StaffUserData(
                u,
                rolesByUser.GetValueOrDefault(u.Id) ?? Array.Empty<StaffRoleData>()))
            .ToList();

        return new StaffPage(items, total);
    }

    // ============================================================
    // CHI TIẾT STAFF
    // ============================================================
    public async Task<StaffUserData?> GetStaffAsync(
        int id,
        bool tracking,
        CancellationToken ct = default)
    {
        // Không lọc UserRoles.IsActive: staff đã khóa vẫn phải tải được để mở lại.
        IQueryable<User> query = _db.Users.Where(u =>
            u.Id == id &&
            u.UserRoles.Any(ur =>
                ur.Role.Name == Roles.Doctor ||
                ur.Role.Name == Roles.Receptionist) &&
            !u.UserRoles.Any(ur =>
                ur.Role.Name == Roles.Admin &&
                ur.IsActive &&
                ur.Role.IsActive));

        if (!tracking)
            query = query.AsNoTracking();

        var user = await query.FirstOrDefaultAsync(ct);
        if (user is null)
            return null;

        var roleRows = await _db.UserRoles
            .AsNoTracking()
            .Where(ur =>
                ur.UserId == id &&
                (ur.Role.Name == Roles.Doctor || ur.Role.Name == Roles.Receptionist))
            .OrderByDescending(ur => ur.IsActive)
            .ThenByDescending(ur => ur.AssignedAt)
            .Select(ur => new
            {
                RoleName = ur.Role.Name,
                AssignmentIsActive = ur.IsActive,
                RoleIsActive = ur.Role.IsActive,
                ur.AssignedAt
            })
            .ToListAsync(ct);

        var roles = roleRows
            .Select(x => new StaffRoleData(
                x.RoleName,
                x.AssignmentIsActive,
                x.RoleIsActive,
                x.AssignedAt))
            .ToList();

        return new StaffUserData(user, roles);
    }

    // ============================================================
    // EMAIL
    // ============================================================
    public Task<bool> EmailExistsAsync(
        string email,
        int? exceptUserId = null,
        CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();

        // Email thuộc về User, không phụ thuộc role đang bị khóa hay mở.
        return _db.Users.AnyAsync(u =>
            u.Email == normalized &&
            (!exceptUserId.HasValue || u.Id != exceptUserId.Value), ct);
    }
    // ============================================================
    // PHONE
    // ============================================================

    public Task<bool> PhoneExistsAsync(
        string phone,
        int? exceptUserId = null,
        CancellationToken ct = default)
    {
        var normalized = phone.Trim();
        return _db.Users.AnyAsync(u =>
            u.Phone == normalized &&
            (!exceptUserId.HasValue || u.Id != exceptUserId.Value), ct);
    }

    // ============================================================
    // ROLE MASTER
    // ============================================================
    public async Task<IReadOnlyList<string>> GetActiveRoleNamesByNamesAsync(
        IEnumerable<string> names,
        CancellationToken ct = default)
    {
        var requested = names
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return await _db.Roles
            .AsNoTracking()
            .Where(r => r.IsActive && requested.Contains(r.Name))
            .Select(r => r.Name)
            .ToListAsync(ct);
    }

    // ============================================================
    // GÁN / ĐỔI STAFF ROLE
    // ============================================================
    public async Task SyncStaffUserRoleAsync(
        User user,
        string roleName,
        int? assignedBy,
        CancellationToken ct = default)
    {
        var normalized = NormalizeManagedStaffRole(roleName)
            ?? throw new ArgumentException(
                "Role nhân viên không hợp lệ.",
                nameof(roleName));

        var targetRole = await _db.Roles
            .FirstOrDefaultAsync(
                r => r.Name == normalized && r.IsActive,
                ct);

        if (targetRole is null)
        {
            throw new InvalidOperationException(
                $"Role '{normalized}' không tồn tại hoặc đã bị vô hiệu hóa.");
        }

        var existing = await _db.UserRoles
            .Where(ur =>
                ur.UserId == user.Id &&
                (ur.Role.Name == Roles.Doctor ||
                 ur.Role.Name == Roles.Receptionist))
            .ToListAsync(ct);

        // Chưa có staff role
        if (existing.Count == 0)
        {
            _db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = targetRole.Id,
                AssignedBy = assignedBy,
                AssignedAt = DateTime.UtcNow,
                IsActive = true
            });

            return;
        }

        // Đã đúng role rồi
        var currentTarget =
            existing.FirstOrDefault(x => x.RoleId == targetRole.Id);

        if (currentTarget is not null)
        {
            currentTarget.IsActive = true;
            currentTarget.AssignedBy = assignedBy;
            currentTarget.AssignedAt = DateTime.UtcNow;

            // Dọn role staff thừa từ dữ liệu cũ
            foreach (var duplicate in existing.Where(x => x != currentTarget))
            {
                _db.UserRoles.Remove(duplicate);
            }

            return;
        }

        // Đổi role:
        // Vì RoleId thuộc PK nên KHÔNG assignment.RoleId = ...
        // Xóa assignment cũ rồi thêm assignment mới.
        _db.UserRoles.RemoveRange(existing);

        _db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = targetRole.Id,
            AssignedBy = assignedBy,
            AssignedAt = DateTime.UtcNow,
            IsActive = true
        });
    }

    // ============================================================
    // USER ĐANG ACTIVE TRONG ROLE
    // ============================================================
    public async Task<IReadOnlyList<User>> GetActiveUsersInRoleAsync(
        string roleName,
        CancellationToken ct = default) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.UserRoles.Any(ur =>
                ur.Role.Name == roleName &&
                ur.Role.IsActive &&
                ur.IsActive))
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

    public Task<bool> IsActiveUserInRoleAsync(
        int userId,
        string roleName,
        CancellationToken ct = default) =>
        _db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == userId &&
            u.UserRoles.Any(ur =>
                ur.Role.Name == roleName &&
                ur.Role.IsActive &&
                ur.IsActive), ct);

    // ============================================================
    // KHÓA / MỞ STAFF ROLE
    // ============================================================
    public async Task<bool> SetStaffRoleActiveAsync(
        int userId,
        string roleName,
        bool isActive,
        int? changedBy,
        CancellationToken ct = default)
    {
        var normalized = NormalizeManagedStaffRole(roleName);
        if (normalized is null)
            return false;

        var assignments = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur =>
                ur.UserId == userId &&
                (ur.Role.Name == Roles.Doctor || ur.Role.Name == Roles.Receptionist))
            .ToListAsync(ct);

        var target = assignments.FirstOrDefault(ur =>
            ur.Role.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));

        if (target is null)
            return false;

        if (isActive && !target.Role.IsActive)
            return false;

        if (!isActive)
        {
            // Khóa staff: tắt mọi staff role, Patient/Admin không bị ảnh hưởng.
            foreach (var assignment in assignments)
                assignment.IsActive = false;

            return true;
        }

        // Mở staff: chỉ mở role được chọn, tránh vô tình phục hồi role cũ.
        foreach (var assignment in assignments)
            assignment.IsActive = assignment.RoleId == target.RoleId;

        target.AssignedAt = DateTime.UtcNow;
        target.AssignedBy = changedBy;
        return true;
    }

    private static string? NormalizeManagedStaffRole(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return null;

        var role = roleName.Trim();
        if (role.Equals(Roles.Doctor, StringComparison.OrdinalIgnoreCase))
            return Roles.Doctor;
        if (role.Equals(Roles.Receptionist, StringComparison.OrdinalIgnoreCase))
            return Roles.Receptionist;
        return null;
    }
}
