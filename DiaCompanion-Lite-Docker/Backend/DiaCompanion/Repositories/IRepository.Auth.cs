using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record AuthUserData(User User, IReadOnlyList<string> Roles, int? PatientId);
public sealed record AuthorizationSnapshot(
    IReadOnlyList<string> Roles,
    int? PatientId,
    bool MustChangePassword,
    string FullName);

public partial interface IRepository
{
    Task<AuthUserData?> GetUserByLoginAsync(string? email, string? phone, CancellationToken ct = default);
    Task<AuthUserData?> GetUserByPhoneAsync(string phone, CancellationToken ct = default);
    Task<AuthUserData?> GetAuthUserByIdAsync(int userId, CancellationToken ct = default);
    Task<AuthorizationSnapshot?> GetAuthorizationSnapshotAsync(int userId, CancellationToken ct = default);
    Task<int?> GetPatientIdByUserIdAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetActiveRoleNamesAsync(int userId, CancellationToken ct = default);
    Task<bool> HasActiveRoleAsync(int userId, string roleName, CancellationToken ct = default);
}
