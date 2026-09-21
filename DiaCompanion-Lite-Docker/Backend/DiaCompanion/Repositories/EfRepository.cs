using DiaCompanion.Api.Common;
using DiaCompanion.Api.Data;
using DiaCompanion.Api.Entities;
using DiaCompanion.Dtos;
using DiaCompanion.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;
using static DiaCompanion.Api.Repositories.IRepository;

namespace DiaCompanion.Api.Repositories;

/// <summary>
/// EF Core implementation. Đây là lớp duy nhất (cùng các partial của nó)
/// được phép làm việc trực tiếp với AppDbContext trong application layer.
/// </summary>
public sealed partial class EfRepository : IRepository
{
    private readonly AppDbContext _db;

    public EfRepository(AppDbContext db) => _db = db;

    public void Add<TEntity>(TEntity entity) where TEntity : class => _db.Set<TEntity>().Add(entity);

    public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class =>
        _db.Set<TEntity>().AddRange(entities);

    public void Remove<TEntity>(TEntity entity) where TEntity : class => _db.Set<TEntity>().Remove(entity);

    public void ApplyOriginalRowVersion<TEntity>(TEntity entity, string rowVersion)
        where TEntity : class
    {
        _db.Entry(entity).Property("RowVer").OriginalValue = RowVersionCodec.Decode(rowVersion);
    }

    public Task<int> CommitAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task<bool> TryCommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) =>
        _db.Database.CanConnectAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(
        Func<Task> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
            try
            {
                await action();
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        TResult? result = default;
        await ExecuteInTransactionAsync(async () =>
        {
            result = await action();
        }, isolationLevel, cancellationToken);
        return result!;
    }



    public Task<User?> GetUserByIdAsync(
        int userId,
        CancellationToken ct = default)
    {
        // Users.IsActive không còn là trạng thái tài khoản.
        return _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
    }

    public Task<bool> UserAlreadyLinkedToActivePatientAsync(int userId, CancellationToken ct = default)
    {
        return _db.Patients.AnyAsync(
        p => p.UserId == userId && !p.IsVoided,
        ct);
    }

    public Task<bool> UserPhoneExistsExceptUserAsync(
        string phone,
        int exceptUserId,
        CancellationToken ct = default)
    {
        return _db.Users.AnyAsync(
            u => u.Phone == phone && u.Id != exceptUserId,
            ct);
    }

    public async Task<IReadOnlyList<LinkableUserDto>>
    GetLinkableUsersForPatientAsync(
        string? keyword,
        int excludedUserId,
        CancellationToken ct = default)
    {
        // 1. Bắt đầu từ Users; trạng thái khóa nằm ở UserRoles, không dùng Users.IsActive.
        var query = BuildLinkablePatientUsersQuery(excludedUserId);


        // 5. Search theo tên / email / phone.
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var q = keyword.Trim();

            query = query.Where(u =>
                u.FullName.Contains(q) ||
                (u.Email != null && u.Email.Contains(q)) ||
                (u.Phone != null && u.Phone.Contains(q)));
        }

        // 6. Chỉ lấy thông tin cần trả cho frontend.
        var users = await query
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.Phone
            })
            .ToListAsync(ct);

        var userIds = users
            .Select(u => u.Id)
            .ToList();

        // 7. Lấy các role active của các User vừa tìm được.
        var roles = await _db.UserRoles
            .Join(
                _db.Roles,
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new
                {
                    UserRole = ur,
                    Role = r
                })
            .Where(x =>
                userIds.Contains(x.UserRole.UserId)
                && x.UserRole.IsActive
                && x.Role.IsActive)
            .Select(x => new
            {
                x.UserRole.UserId,
                RoleName = x.Role.Name
            })
            .AsNoTracking()
            .ToListAsync(ct);

        // 8. Ghép User + Roles thành DTO.
        return users
            .Select(u => new LinkableUserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                Roles = roles
                    .Where(r => r.UserId == u.Id)
                    .Select(r => r.RoleName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .ToList();
    }

    private IQueryable<User> BuildLinkablePatientUsersQuery(
    int excludedUserId)
    {
        return _db.Users

            // Global User filter tự loại User.IsVoided = true.

            // Không cho lễ tân đang thao tác tự chọn mình.
            .Where(u => u.Id != excludedUserId)

            // User không được đang liên kết Patient active.
            //
            // Không IgnoreQueryFilters:
            // Patient đã void sẽ tự bị bỏ.
            .Where(u =>
                !_db.Patients.Any(p =>
                    p.UserId == u.Id))

            // Đây chỉ là luồng reuse tài khoản STAFF.
            .Where(u =>
                u.UserRoles.Any(ur =>
                    (
                        ur.Role.Name == Roles.Doctor ||
                        ur.Role.Name == Roles.Receptionist
                    )
                    && ur.IsActive
                    && ur.Role.IsActive))

            // Patient role phải đang tắt.
            .Where(u =>
                !u.UserRoles.Any(ur =>
                    ur.Role.Name == Roles.Patient &&
                    ur.IsActive &&
                    ur.Role.IsActive))

            // Không cho Admin đi qua màn này.
            .Where(u =>
                !u.UserRoles.Any(ur =>
                    ur.Role.Name == Roles.Admin &&
                    ur.IsActive &&
                    ur.Role.IsActive));
    }
    public async Task<MedicalRecord?> GetActiveMedicalRecordByPatientIdAsync(
       int patientId,
       bool tracking = false,
       CancellationToken ct = default)
    {
        IQueryable<MedicalRecord> query = _db.MedicalRecords
            .Where(mr => mr.PatientId == patientId);

        // MedicalRecord đã có global query filter !IsVoided,
        // nên không cần viết lại mr.IsVoided == false ở đây.

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(ct);
    }


    public async Task<Patient?> GetPatientByIdAsync(
        int patientId,
        bool tracking = false,
        CancellationToken ct = default)
    {
        IQueryable<Patient> query = _db.Patients
            .Where(p => p.Id == patientId);

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(ct);
    }



    public async Task<PatientAdminTarget?> GetPatientAdminTargetAsync(
    int patientId,
    bool tracking,
    CancellationToken ct = default)
    {
        IQueryable<Patient> query = _db.Patients
            .Include(p => p.User)
            .Where(p => p.Id == patientId);

        if (!tracking)
            query = query.AsNoTracking();

        var patient = await query.FirstOrDefaultAsync(ct);

        if (patient is null)
            return null;


        if (patient.UserId is not int userId)
        {
            return new PatientAdminTarget(
                patient,
                null,
                null);
        }


        IQueryable<UserRole> roleQuery = _db.UserRoles
            .Where(ur =>
                ur.UserId == userId &&
                ur.Role.Name == Roles.Patient);

        if (!tracking)
            roleQuery = roleQuery.AsNoTracking();

        var patientRole =
            await roleQuery.FirstOrDefaultAsync(ct);


        return new PatientAdminTarget(
            patient,
            patient.User,
            patientRole);
    }

    public Task<bool> IsUserLinkableToPatientAsync(
    int userId,
    int excludedUserId,
    CancellationToken ct = default)
    {
        return BuildLinkablePatientUsersQuery(excludedUserId)
            .AnyAsync(
                u => u.Id == userId,
                ct);
    }
    public Task<Patient?> GetPatientForRiskAsync(int patientId, CancellationToken ct = default) =>
    _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId, ct);

    public async Task<decimal?> GetLatestHba1cAsync(int patientId, CancellationToken ct = default) =>
        await _db.HealthMetrics.AsNoTracking()
            .Where(m => m.PatientId == patientId && m.MetricType == MetricType.HbA1c)
            .OrderByDescending(m => m.RecordedAtUtc).ThenByDescending(m => m.Id)
            .Select(m => (decimal?)m.Value)
            .FirstOrDefaultAsync(ct);

    public async Task<decimal?> GetRecentSystolicAverageAsync(
        int patientId, int sampleSize, CancellationToken ct = default)
    {
        var values = await _db.HealthMetrics.AsNoTracking()
            .Where(m => m.PatientId == patientId && m.MetricType == MetricType.SystolicBp)
            .OrderByDescending(m => m.RecordedAtUtc).ThenByDescending(m => m.Id)
            .Take(sampleSize)
            .Select(m => m.Value)
            .ToListAsync(ct);

        // Dưới 3 lần đo thì không đủ cơ sở kết luận kiểm soát HA -> trả null (fail-safe).
        return values.Count < sampleSize ? null : values.Average();
    }
}
