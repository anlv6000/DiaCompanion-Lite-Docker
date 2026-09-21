using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record StaffRoleData(
    string Name,
    bool IsActive,
    bool RoleIsActive,
    DateTime AssignedAt);

public sealed record StaffUserData(
    User User,
    IReadOnlyList<StaffRoleData> Roles);

public sealed record StaffPage(
    IReadOnlyList<StaffUserData> Items,
    int Total);

public partial interface IRepository
{
    Task<StaffPage> GetStaffPageAsync(
        string? q,
        string? roleName,
        bool? isActive,
        PageQuery page,
        CancellationToken ct = default);

    Task<StaffUserData?> GetStaffAsync(
        int id,
        bool tracking,
        CancellationToken ct = default);

    Task<bool> EmailExistsAsync(
        string email,
        int? exceptUserId = null,
        CancellationToken ct = default);
    Task<bool> PhoneExistsAsync(
        string phone,
        int? exceptUserId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetActiveRoleNamesByNamesAsync(
        IEnumerable<string> names,
        CancellationToken ct = default);

    Task SyncStaffUserRoleAsync(
        User user,
        string roleName,
        int? assignedBy,
        CancellationToken ct = default);

    Task<IReadOnlyList<User>> GetActiveUsersInRoleAsync(
        string roleName,
        CancellationToken ct = default);

    Task<bool> IsActiveUserInRoleAsync(
        int userId,
        string roleName,
        CancellationToken ct = default);

    Task<bool> SetStaffRoleActiveAsync(
        int userId,
        string roleName,
        bool isActive,
        int? changedBy,
        CancellationToken ct = default);
}
