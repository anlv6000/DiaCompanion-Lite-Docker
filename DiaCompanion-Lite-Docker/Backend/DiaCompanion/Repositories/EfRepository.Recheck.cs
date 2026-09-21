using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public async Task<IReadOnlyList<RecheckCandidate>> GetRecheckCandidatesAsync(
    CancellationToken ct = default)
    {
        // 1. Lấy tất cả lượt khám đã hoàn thành, chưa void
        var allCompleted = await _db.Visits
            .AsNoTracking()
            .Where(v =>
                !v.IsVoided &&
                v.Status == VisitStatus.Completed &&
                v.ClosedAt != null)
            .Select(v => new
            {
                v.Id,
                v.MedicalRecord.PatientId,
                v.VisitDate,
                ClosedAt = v.ClosedAt!.Value,
                v.RecheckMonths,
                v.Referral,

                v.MedicalRecord.Patient!.Code,
                v.MedicalRecord.Patient.FullName,
                v.MedicalRecord.Patient.Phone
            })
            .ToListAsync(ct);

        if (allCompleted.Count == 0)
            return Array.Empty<RecheckCandidate>();

        // 2. Mỗi bệnh nhân chỉ lấy lượt khám Completed mới nhất
        var lastVisits = allCompleted
            .GroupBy(v => v.PatientId)
            .Select(g => g
                .OrderByDescending(v => v.ClosedAt)
                .ThenByDescending(v => v.Id)
                .First())
            // Lượt khám mới nhất phải có chỉ định tái tầm soát
            .Where(v => v.RecheckMonths.HasValue)
            .ToList();

        if (lastVisits.Count == 0)
            return Array.Empty<RecheckCandidate>();

        // 3. Lấy các VisitId cần tìm grade
        var visitIds = lastVisits
            .Select(v => v.Id)
            .ToArray();

        // 4. Với mỗi Visit lấy FinalGrade lớn nhất
        var grades = await _db.DiagnosisReviews
            .AsNoTracking()
            .Where(r =>
                !r.IsVoided &&
                r.AiDiagnosis != null &&
                r.AiDiagnosis.FundusImage != null &&
                r.AiDiagnosis.FundusImage.VisitId.HasValue &&
                visitIds.Contains(
                    r.AiDiagnosis.FundusImage.VisitId.Value))
            .GroupBy(r =>
                r.AiDiagnosis!.FundusImage!.VisitId!.Value)
            .Select(g => new
            {
                VisitId = g.Key,
                Grade = (byte)g.Max(x => (byte)x.FinalGrade)
            })
            .ToDictionaryAsync(
                x => x.VisitId,
                x => x.Grade,
                ct);

        // 5. Tạo candidate
        return lastVisits
            .Select(v => new RecheckCandidate(
                v.PatientId,
                v.Code,
                v.FullName,
                v.Phone,
                v.Id,
                v.ClosedAt,
                v.RecheckMonths!.Value,
                v.Referral,

                // Nếu record hiện tại vẫn bắt buộc field LatestVisitDate
                // thì truyền VisitDate của chính lượt khám mới nhất.
                v.VisitDate,

                grades.TryGetValue(v.Id, out var grade)
                    ? grade
                    : null
            ))
            .ToList();
    }
    public async Task<RecheckCandidate?> GetRecheckCandidateAsync(int patientId, CancellationToken ct = default)
    {
        var visit = await _db.Visits.AsNoTracking()
            .Where(v => !v.IsVoided && v.MedicalRecord.PatientId == patientId && v.Status == VisitStatus.Completed &&
                        v.ClosedAt != null && v.RecheckMonths != null)
            .OrderByDescending(v => v.ClosedAt)
            .Select(v => new
            {
                v.Id,
                PatientId = v.MedicalRecord.PatientId,
                ClosedAt = v.ClosedAt!.Value,
                Months = v.RecheckMonths!.Value,
                v.Referral,
                v.MedicalRecord.Patient!.Code,
                v.MedicalRecord.Patient.FullName,
                v.MedicalRecord.Patient.Phone
            }).FirstOrDefaultAsync(ct);
        if (visit is null) return null;

        var latestVisitDate = await _db.Visits.AsNoTracking()
            .Where(v => !v.IsVoided
                        && v.MedicalRecord.PatientId == patientId
                        && (v.RecheckMonths != null
                            || v.Images.Any(i => !i.IsVoided)
                            || _db.Prescriptions.Any(p => p.VisitId == v.Id && !p.IsVoided)))
            .MaxAsync(v => (DateTime?)v.VisitDate, ct);
        var grade = await _db.DiagnosisReviews.AsNoTracking()
            .Where(r => !r.IsVoided && r.AiDiagnosis != null && r.AiDiagnosis.FundusImage != null &&
                        r.AiDiagnosis.FundusImage.VisitId == visit.Id)
            .Select(r => (byte?)(byte)r.FinalGrade).MaxAsync(ct);

        return new RecheckCandidate(
            visit.PatientId, visit.Code, visit.FullName, visit.Phone, visit.Id,
            visit.ClosedAt, visit.Months, visit.Referral, latestVisitDate, grade);
    }
}
