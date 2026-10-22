using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

/// <summary>Một dòng của danh sách gói chấm (1.3.1).</summary>
public sealed record GradingPackageSummary(
    int Id,
    string Code,
    byte Round,
    int GraderId,
    string GraderName,
    string? GraderEmail,
    GradingPackageStatus Status,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    string? Note,
    int ItemCount,
    int ImportedCount);

/// <summary>Ảnh của một mục trong gói, để đóng gói tệp zip. FilePath null = ảnh đã bị thu hồi.</summary>
public sealed record GradingPackageFile(int ItemId, string Token, int Position, int FundusImageId, string? FilePath);

/// <summary>Tài khoản người chấm (vai trò Grader đang hoạt động).</summary>
public sealed record GraderAccount(int Id, string FullName, string? Email);

public partial interface IRepository
{
    /// <summary>Ảnh đang nằm trong một gói CÒN MỞ của vòng này và chưa được nhập kết quả.
    /// Những ảnh này không vào hàng chờ chấm trong ứng dụng và không vào gói khác.</summary>
    Task<HashSet<int>> GetImageIdsReservedByOpenPackagesAsync(byte round, CancellationToken ct = default);

    Task<IReadOnlyList<GradingPackageSummary>> GetGradingPackagesAsync(CancellationToken ct = default);

    /// <summary>Gói kèm danh sách mục (tracking để cập nhật khi nhập kết quả).</summary>
    Task<GradingPackage?> GetGradingPackageForUpdateAsync(int id, CancellationToken ct = default);
    Task<GradingPackage?> GetGradingPackageByCodeForUpdateAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<GradingPackageFile>> GetGradingPackageFilesAsync(int packageId, CancellationToken ct = default);

    Task<bool> GradingPackageCodeExistsAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<GraderAccount>> GetGraderAccountsAsync(CancellationToken ct = default);
    Task<GraderAccount?> GetGraderAccountAsync(int userId, CancellationToken ct = default);

    /// <summary>Id các ảnh còn hiệu lực trong danh sách (ảnh đã thu hồi bị loại).</summary>
    Task<HashSet<int>> GetActiveImageIdsAsync(IReadOnlyCollection<int> imageIds, CancellationToken ct = default);
}
