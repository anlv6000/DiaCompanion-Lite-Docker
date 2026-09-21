using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;
using DiaCompanion.Dtos;
using DiaCompanion.Entities;

namespace DiaCompanion.Api.Repositories;

/// <summary>
/// Cổng duy nhất từ tầng Service sang persistence. Service không nhận DbContext,
/// DbSet, IQueryable, DatabaseFacade hay EntityEntry. Mọi LINQ/EF Core nằm trong
/// các file partial EfRepository.* thuộc tầng Repository.
/// </summary>
public partial interface IRepository : IUnitOfWork
{
    void Add<TEntity>(TEntity entity) where TEntity : class;
    void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
    void Remove<TEntity>(TEntity entity) where TEntity : class;

    void ApplyOriginalRowVersion<TEntity>(TEntity entity, string rowVersion)
        where TEntity : class;

    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);

    Task<User?> GetUserByIdAsync(int userId, CancellationToken ct = default);

    Task<bool> UserAlreadyLinkedToActivePatientAsync(int userId, CancellationToken ct = default);

    Task<bool> UserPhoneExistsExceptUserAsync(string phone, int exceptUserId, CancellationToken ct = default);

    Task<IReadOnlyList<LinkableUserDto>> GetLinkableUsersForPatientAsync(string? keyword, int excludedUserId, CancellationToken ct = default);
    Task<MedicalRecord?> GetActiveMedicalRecordByPatientIdAsync(int patientId, bool tracking = false, CancellationToken ct = default);
    Task<Patient?> GetPatientByIdAsync(
        int patientId,
        bool tracking = false,
        CancellationToken ct = default);
    Task<bool> IsUserLinkableToPatientAsync(
    int userId,
    int excludedUserId,
    CancellationToken ct = default);

    Task<AdminPatientPage> GetAdminPatientPageAsync(
    string? q,
    string? status,
    PageQuery page,
    CancellationToken ct = default);

    Task<PatientAdminTarget?> GetPatientAdminTargetAsync(
        int patientId,
        bool tracking,
        CancellationToken ct = default);
    Task<Patient?> GetPatientForRiskAsync(int patientId, CancellationToken ct = default);
    Task<decimal?> GetLatestHba1cAsync(int patientId, CancellationToken ct = default);
    Task<decimal?> GetRecentSystolicAverageAsync(int patientId, int sampleSize, CancellationToken ct = default);
}
