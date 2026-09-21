using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record HealthMetricPage(
    IReadOnlyList<HealthMetric> Items,
    IReadOnlyList<HealthMetric> BloodPressureRows,
    bool HasMore);

public partial interface IRepository
{
    Task<HealthMetricPage> GetHealthMetricPageAsync(
        int patientId,
        MetricType? type,
        DateOnly? from,
        DateOnly? to,
        DateTime? cursorAt,
        long? cursorId,
        int size,
        CancellationToken ct = default);

    Task<HealthMetric?> GetHealthMetricForUpdateAsync(int id, CancellationToken ct = default);
    Task<HealthMetric?> GetBloodPressurePairForUpdateAsync(
        int patientId, int? visitId, DateTime recordedAtUtc, MetricType pairType, CancellationToken ct = default);
    Task<IReadOnlyList<HealthMetric>> GetHealthMetricsAsync(
        int patientId, DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<IReadOnlyList<LifestyleLog>> GetLifestyleLogsAsync(
        int patientId, DateOnly from, CancellationToken ct = default);
    Task<LifestyleLog?> GetLifestyleLogForUpdateAsync(
        int id, int? patientId = null, CancellationToken ct = default);

    Task<IReadOnlyList<MedicationLog>> GetMedicationLogsForDateAsync(
        int patientId, DateOnly date, CancellationToken ct = default);
    Task<MedicationLog?> GetMedicationLogForUpdateAsync(int id, CancellationToken ct = default);
}
