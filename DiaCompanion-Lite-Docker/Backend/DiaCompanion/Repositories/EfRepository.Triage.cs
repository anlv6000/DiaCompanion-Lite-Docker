using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public async Task<TriageQueuePage> GetTriageQueueAsync(
        int? currentDoctorId, int? filterDoctorId, bool? deferredOnly, string? q,
        DateTime? cursorAt, long? cursorId, int size, CancellationToken ct = default)
    {
        var query = _db.AiDiagnoses.AsNoTracking()
            .Where(d => !d.Reviews.Any())
            .Select(d => new
            {
                d.Id, d.DrGrade, d.Disagreement, d.ClinicalRiskScore, d.IsDeferred, d.DeferReason,
                d.CreatedAt, d.RowVer,
                Eye = d.FundusImage!.Eye,
                VisitId = d.FundusImage.VisitId,
                PatientId = d.FundusImage.PatientId,
                PatientCode = d.FundusImage.Patient!.Code,
                PatientName = d.FundusImage.Patient.FullName,
                PatientNameSearch = d.FundusImage.Patient.FullNameSearch,
                DoctorId = d.FundusImage.Visit != null ? d.FundusImage.Visit.DoctorId : null,
                DoctorName = d.FundusImage.Visit != null && d.FundusImage.Visit.Doctor != null
                    ? d.FundusImage.Visit.Doctor.FullName : null
            });

        if (currentDoctorId is int current) query = query.Where(x => x.DoctorId == current);
        else if (filterDoctorId is int filter) query = query.Where(x => x.DoctorId == filter);
        if (deferredOnly == true) query = query.Where(x => x.IsDeferred);
        if (!string.IsNullOrWhiteSpace(q) && q.Trim().Length >= 2)
        {
            var text = q.Trim();
            var norm = VietnameseText.RemoveDiacritics(text);
            query = query.Where(x =>
                EF.Functions.Like(x.PatientNameSearch!, $"%{norm}%") ||
                EF.Functions.Like(x.PatientCode, $"%{text}%"));
        }
        if (cursorAt is DateTime at && cursorId is long lastId)
            query = query.Where(x => x.CreatedAt < at || (x.CreatedAt == at && x.Id < lastId));

        var rows = await query.OrderByDescending(x => x.IsDeferred)
            .ThenByDescending(x => x.ClinicalRiskScore ?? 0)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(size + 1)
            .ToListAsync(ct);
        var hasMore = rows.Count > size;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        return new TriageQueuePage(rows.Select(x => new TriageQueueRow(
            x.Id, x.DrGrade, x.Disagreement, x.IsDeferred, x.DeferReason,
            x.CreatedAt, x.RowVer, x.Eye, x.VisitId, x.PatientId, x.PatientCode,
            x.PatientName, x.DoctorId, x.DoctorName)
        {
            ClinicalRiskScore = x.ClinicalRiskScore
        }).ToList(), hasMore);
    }

    public async Task<TriageCounts> GetTriageCountsAsync(int? currentDoctorId, CancellationToken ct = default)
    {
        var query = _db.AiDiagnoses.AsNoTracking().Where(d => !d.Reviews.Any());
        if (currentDoctorId is int doctorId)
            query = query.Where(d => d.FundusImage!.Visit != null && d.FundusImage.Visit.DoctorId == doctorId);
        var pending = await query.CountAsync(ct);
        var deferred = await query.CountAsync(d => d.IsDeferred, ct);
        return new TriageCounts(pending, deferred);
    }

    public Task<AiDiagnosis?> GetDiagnosisForReviewAsync(int diagnosisId, CancellationToken ct = default) =>
        _db.AiDiagnoses.Include(x => x.FundusImage).ThenInclude(x => x!.Visit)
            .FirstOrDefaultAsync(x => x.Id == diagnosisId, ct);

    public Task<bool> ReviewExistsForDiagnosisAsync(int diagnosisId, CancellationToken ct = default) =>
        _db.DiagnosisReviews.AsNoTracking().AnyAsync(r => r.AiDiagnosisId == diagnosisId, ct);

    public async Task<bool> TryCommitReviewAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627)
        {
            return false;
        }
    }

    public Task<DiagnosisReview?> GetReviewAsync(int reviewId, CancellationToken ct = default) =>
        _db.DiagnosisReviews.AsNoTracking().Include(x => x.Doctor)
            .FirstOrDefaultAsync(x => x.Id == reviewId, ct);

   


}
