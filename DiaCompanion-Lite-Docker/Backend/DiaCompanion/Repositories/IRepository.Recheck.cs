using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Repositories;

public sealed record RecheckCandidate(
    int PatientId,
    string PatientCode,
    string PatientName,
    string PatientPhone,
    int LastVisitId,
    DateTime ClosedAt,
    byte RecheckMonths,
    ReferralType? Referral,
    DateTime? LatestVisitDate,
    byte? LastConfirmedGrade);

public partial interface IRepository
{
    Task<IReadOnlyList<RecheckCandidate>> GetRecheckCandidatesAsync(CancellationToken ct = default);
    Task<RecheckCandidate?> GetRecheckCandidateAsync(int patientId, CancellationToken ct = default);
}
