using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public Task MarkOverdueMedicationLogsMissedAsync(DateTime missedBefore, CancellationToken ct = default) =>
        _db.MedicationLogs
            .Where(x => x.Status == MedicationStatus.Pending && x.ScheduledAt < missedBefore)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, MedicationStatus.Missed), ct);

    public async Task<IReadOnlyList<MedicationLog>> GetMedicationReminderCandidatesAsync(
        DateTime missedBefore, DateTime remindUntil, int take, CancellationToken ct = default) =>
        await _db.MedicationLogs
            .Include(x => x.PrescriptionItem)
                .ThenInclude(x => x!.Prescription)
                    .ThenInclude(x => x!.Patient)
            .Where(x => x.Status == MedicationStatus.Pending
                        && x.ReminderSentAt == null
                        && x.ScheduledAt >= missedBefore
                        && x.ScheduledAt <= remindUntil
                        && x.PrescriptionItem != null
                        && x.PrescriptionItem.IsActive
                        && x.PrescriptionItem.Prescription != null
                        && !x.PrescriptionItem.Prescription.IsVoided)
            .OrderBy(x => x.ScheduledAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RecheckReminderCandidate>> GetRecheckReminderCandidatesAsync(
        CancellationToken ct = default)
    {
        var rows = await _db.Visits.AsNoTracking()
            .Where(v => !v.IsVoided && v.Status == VisitStatus.Completed
                        && v.ClosedAt != null
                        && v.RecheckMonths != null
                        && v.MedicalRecord.Patient != null
                        && v.MedicalRecord.Patient.UserId != null)
            .Select(v => new
            {
                v.Id,
                PatientId =
                v.MedicalRecord.PatientId,
                UserId = v.MedicalRecord.Patient!.UserId!.Value,
                PatientName = v.MedicalRecord.Patient.FullName,
                ClosedAt = v.ClosedAt!.Value,
                RecheckMonths = v.RecheckMonths!.Value
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return Array.Empty<RecheckReminderCandidate>();

        var latestVisits = rows
            .GroupBy(x => x.PatientId)
            .Select(g => g.OrderByDescending(x => x.ClosedAt).ThenByDescending(x => x.Id).First())
            .ToList();

        var patientIds = latestVisits.Select(x => x.PatientId).ToArray();
        var latestDates = await _db.Visits.AsNoTracking()
            .Where(v => !v.IsVoided
                        && patientIds.Contains(v.MedicalRecord.PatientId)
                        && (v.RecheckMonths != null
                            || v.Images.Any(i => !i.IsVoided)
                            || _db.Prescriptions.Any(p => p.VisitId == v.Id && !p.IsVoided)))
            .GroupBy(v => v.MedicalRecord.PatientId)
            .Select(g => new { PatientId = g.Key, LatestVisitDate = g.Max(v => v.VisitDate) })
            .ToDictionaryAsync(x => x.PatientId, x => x.LatestVisitDate, ct);

        return latestVisits.Select(v => new RecheckReminderCandidate(
            v.Id,
            v.PatientId,
            v.UserId,
            v.PatientName,
            v.ClosedAt,
            v.RecheckMonths,
            latestDates.GetValueOrDefault(v.PatientId)))
            .ToList();
    }

    public Task<bool> NotificationExistsAsync(
        int userId, NotificationType type, string title, string linkEntity, int linkEntityId,
        CancellationToken ct = default) =>
        _db.Notifications.AsNoTracking().AnyAsync(n =>
            n.UserId == userId
            && n.Type == type
            && n.Title == title
            && n.LinkEntity == linkEntity
            && n.LinkEntityId == linkEntityId, ct);
}
