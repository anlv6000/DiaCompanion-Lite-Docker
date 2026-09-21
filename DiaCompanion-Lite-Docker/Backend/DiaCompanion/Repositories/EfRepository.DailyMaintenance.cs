using DiaCompanion.Api.Common;
using Microsoft.EntityFrameworkCore;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public async Task<IReadOnlyList<OpenVisitMaintenanceCandidate>> GetOpenVisitMaintenanceCandidatesAsync(
        DateTime cutoffExclusiveUtc,
        CancellationToken ct = default)
    {
        return await _db.Visits.AsNoTracking()
            .Where(v => !v.IsVoided
                        && v.Status == VisitStatus.InProgress
                        && v.VisitDate < cutoffExclusiveUtc)
            .OrderBy(v => v.VisitDate)
            .Select(v => new OpenVisitMaintenanceCandidate(
                v.Id,
                v.MedicalRecord.PatientId,
                v.MedicalRecord.Patient != null ? v.MedicalRecord.Patient.UserId : null,
                v.DoctorId,
                v.VisitDate,
                v.Images.Any(i => !i.IsVoided)
                || _db.Prescriptions.Any(p => p.VisitId == v.Id && !p.IsVoided)
                || _db.Feedbacks.Any(f => f.VisitId == v.Id)
                || v.Conclusion != null
                || v.Referral != null
                || v.RecheckMonths != null))
            .ToListAsync(ct);
    }

    public Task<DiaCompanion.Api.Entities.Visit?> GetOpenVisitForDailyMaintenanceAsync(
        int visitId,
        CancellationToken ct = default) =>
        _db.Visits
            .Include(v => v.MedicalRecord)
                .ThenInclude(m => m.Patient)
            .FirstOrDefaultAsync(v => v.Id == visitId
                                      && !v.IsVoided
                                      && v.Status == VisitStatus.InProgress,
                ct);
}
