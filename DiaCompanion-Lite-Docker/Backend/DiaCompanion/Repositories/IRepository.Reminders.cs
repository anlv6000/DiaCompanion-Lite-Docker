using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record RecheckReminderCandidate(
    int VisitId,
    int PatientId,
    int UserId,
    string PatientName,
    DateTime ClosedAt,
    byte RecheckMonths,
    DateTime? LatestVisitDate);

public partial interface IRepository
{
    Task MarkOverdueMedicationLogsMissedAsync(DateTime missedBefore, CancellationToken ct = default);
    Task<IReadOnlyList<MedicationLog>> GetMedicationReminderCandidatesAsync(
        DateTime missedBefore, DateTime remindUntil, int take, CancellationToken ct = default);
    Task<IReadOnlyList<RecheckReminderCandidate>> GetRecheckReminderCandidatesAsync(CancellationToken ct = default);
    Task<bool> NotificationExistsAsync(
        int userId, NotificationType type, string title, string linkEntity, int linkEntityId,
        CancellationToken ct = default);
}
