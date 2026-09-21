using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record GradeCount(byte Grade, int Count);
public sealed record DashboardStats(
    int TotalDiagnoses,
    int DeferredTotal,
    int TotalReviews,
    int Overrides,
    int ClosedVisits,
    int Referred,
    int TotalPatients,
    int Visits,
    int PendingTriage,
    int DeferredPending,
    IReadOnlyList<GradeCount> GradeDistribution,
    string? ActiveModel);

public sealed record DiagnosisThresholdRow(decimal? Disagreement, bool IsDeferred);
public sealed record ModelVersionWithCount(ModelVersion Model, int DiagnosisCount);
public sealed record AuditPage(IReadOnlyList<AuditLog> Items, bool HasMore);

public partial interface IRepository
{
    Task<DashboardStats> GetDashboardStatsAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        int? modelVersionId,
        int? doctorId,
        bool countAllPatients,
        CancellationToken ct = default);

    Task<IReadOnlyList<SystemConfig>> GetSystemConfigsAsync(CancellationToken ct = default);
    Task<SystemConfig?> GetSystemConfigForUpdateAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<DiagnosisThresholdRow>> GetDiagnosisThresholdRowsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ModelVersionWithCount>> GetModelVersionsWithCountsAsync(CancellationToken ct = default);
    Task<ModelVersion?> GetModelVersionAsync(int id, bool tracking, CancellationToken ct = default);
    Task<bool> ModelNameExistsAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<ModelVersion>> GetOtherActiveModelsForUpdateAsync(int excludedModelId, ModelType modelType, CancellationToken ct = default);
    Task<bool> ModelHasDiagnosesAsync(int modelVersionId, CancellationToken ct = default);
    Task<int> CountDiagnosesForModelAsync(int modelVersionId, CancellationToken ct = default);

    Task<AuditPage> GetAuditPageAsync(
        string? action,
        string? entityType,
        int? entityId,
        int? userId,
        DateTime? from,
        DateTime? to,
        DateTime? cursorAt,
        long? cursorId,
        int size,
        CancellationToken ct = default);
}
