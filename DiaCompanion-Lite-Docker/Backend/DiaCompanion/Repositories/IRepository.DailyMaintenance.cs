using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record OpenVisitMaintenanceCandidate(
    int VisitId,
    int PatientId,
    int? PatientUserId,
    int? DoctorId,
    DateTime VisitDate,
    bool HasClinicalData);

public partial interface IRepository
{
    Task<IReadOnlyList<OpenVisitMaintenanceCandidate>> GetOpenVisitMaintenanceCandidatesAsync(
        DateTime cutoffExclusiveUtc,
        CancellationToken ct = default);

    Task<Visit?> GetOpenVisitForDailyMaintenanceAsync(int visitId, CancellationToken ct = default);
}
