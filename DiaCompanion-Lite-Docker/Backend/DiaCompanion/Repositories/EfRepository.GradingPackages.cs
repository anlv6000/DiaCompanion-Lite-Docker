using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public async Task<HashSet<int>> GetImageIdsReservedByOpenPackagesAsync(byte round, CancellationToken ct = default)
    {
        var ids = await _db.GradingPackageItems.AsNoTracking()
            .Where(i => i.BlindGradeId == null
                        && i.Package!.Round == round
                        && i.Package.Status == GradingPackageStatus.Open)
            .Select(i => i.FundusImageId)
            .Distinct()
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    public async Task<IReadOnlyList<GradingPackageSummary>> GetGradingPackagesAsync(CancellationToken ct = default) =>
        await _db.GradingPackages.AsNoTracking()
            .OrderByDescending(p => p.Id)
            .Select(p => new GradingPackageSummary(
                p.Id, p.Code, p.Round, p.GraderId,
                p.Grader != null ? p.Grader.FullName : "",
                p.Grader != null ? p.Grader.Email : null,
                p.Status, p.CreatedAt, p.ClosedAt, p.Note,
                p.Items.Count(),
                p.Items.Count(i => i.BlindGradeId != null)))
            .ToListAsync(ct);

    public Task<GradingPackage?> GetGradingPackageForUpdateAsync(int id, CancellationToken ct = default) =>
        _db.GradingPackages.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<GradingPackage?> GetGradingPackageByCodeForUpdateAsync(string code, CancellationToken ct = default) =>
        _db.GradingPackages.Include(p => p.Items).FirstOrDefaultAsync(p => p.Code == code, ct);

    public async Task<IReadOnlyList<GradingPackageFile>> GetGradingPackageFilesAsync(int packageId, CancellationToken ct = default)
    {
        // Join trái tới FundusImages (có query filter): ảnh đã thu hồi sau khi tạo gói cho FilePath = null.
        var rows = await (
            from i in _db.GradingPackageItems.AsNoTracking()
            where i.PackageId == packageId
            join f in _db.FundusImages.AsNoTracking() on i.FundusImageId equals f.Id into fs
            from f in fs.DefaultIfEmpty()
            orderby i.Position
            select new { i.Id, i.Token, i.Position, i.FundusImageId, FilePath = f != null ? f.FilePath : null })
            .ToListAsync(ct);
        return rows.Select(r => new GradingPackageFile(r.Id, r.Token, r.Position, r.FundusImageId, r.FilePath)).ToList();
    }

    public Task<bool> GradingPackageCodeExistsAsync(string code, CancellationToken ct = default) =>
        _db.GradingPackages.AsNoTracking().AnyAsync(p => p.Code == code, ct);

    // Lọc trên User TRƯỚC khi chiếu sang record GraderAccount: EF Core không dịch được Where/OrderBy
    // đặt sau Select(new GraderAccount(...)) (lỗi 500 khi tạo gói ở bản kiểm thử đầu của 1.3.1).
    private IQueryable<User> GraderUsers() =>
        _db.Users.AsNoTracking()
            .Where(u => !u.IsVoided && u.UserRoles.Any(ur => ur.IsActive && ur.Role.IsActive && ur.Role.Name == Roles.Grader));

    public async Task<IReadOnlyList<GraderAccount>> GetGraderAccountsAsync(CancellationToken ct = default) =>
        await GraderUsers()
            .OrderBy(u => u.FullName)
            .Select(u => new GraderAccount(u.Id, u.FullName, u.Email))
            .ToListAsync(ct);

    public Task<GraderAccount?> GetGraderAccountAsync(int userId, CancellationToken ct = default) =>
        GraderUsers()
            .Where(u => u.Id == userId)
            .Select(u => new GraderAccount(u.Id, u.FullName, u.Email))
            .FirstOrDefaultAsync(ct);

    public async Task<HashSet<int>> GetActiveImageIdsAsync(IReadOnlyCollection<int> imageIds, CancellationToken ct = default)
    {
        if (imageIds.Count == 0) return new HashSet<int>();
        var ids = await _db.FundusImages.AsNoTracking()
            .Where(f => imageIds.Contains(f.Id))
            .Select(f => f.Id)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }
}
