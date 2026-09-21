using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record PrescriptionPage(IReadOnlyList<Prescription> Items, int Total);
public sealed record PrescriptionMedicationStats(int Total, int Taken, int Missed, int Skipped);

public partial interface IRepository
{
    Task<PrescriptionPage> GetPrescriptionPageAsync(
        int patientId,
        string? keyword,
        DateTime? fromUtc,
        DateTime? toExclusiveUtc,
        bool? voided,
        PageQuery page,
        CancellationToken ct = default);

    Task<Prescription?> GetPrescriptionAsync(int id, bool tracking, CancellationToken ct = default);
    Task<IReadOnlyList<MedicationLog>> GetPendingMedicationLogsForItemsAsync(IEnumerable<int> prescriptionItemIds, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, PrescriptionMedicationStats>> GetPrescriptionMedicationStatsAsync(IEnumerable<int> prescriptionIds, CancellationToken ct = default);
}
