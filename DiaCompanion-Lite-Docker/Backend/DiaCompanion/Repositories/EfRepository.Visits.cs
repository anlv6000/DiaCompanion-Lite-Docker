using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public async Task<VisitPage> GetVisitPageAsync(int? patientId, int? doctorId, byte? status,
        DateTime? fromUtc, DateTime? toExclusiveUtc, PageQuery page, CancellationToken ct = default)
    {
        var query = _db.Visits.AsNoTracking().AsQueryable();
        if (patientId is int pid) query = query.Where(v => v.MedicalRecord.PatientId == pid);
        //v.PatientId == pid
        if (doctorId is int did) query = query.Where(v => v.DoctorId == did);
        if (status is byte st) query = query.Where(v => (byte)v.Status == st);
        if (fromUtc is DateTime from) query = query.Where(v => v.VisitDate >= from);
        if (toExclusiveUtc is DateTime to) query = query.Where(v => v.VisitDate < to);

        var total = await query.CountAsync(ct);

        var items = await query
        .OrderBy(v =>
            v.Status == VisitStatus.InProgress ? 0 : 1)
        .ThenBy(v =>
            v.Status == VisitStatus.InProgress
                ? (DateTime?)v.VisitDate
                : null)
        .ThenByDescending(v =>
            v.Status == VisitStatus.Completed
                ? (DateTime?)v.VisitDate
                : null)
        .ThenBy(v =>
            v.Status == VisitStatus.InProgress
                ? (int?)v.Id
                : null)
        .ThenByDescending(v =>
            v.Status == VisitStatus.Completed
                ? (int?)v.Id
                : null)
        .Skip(page.Skip)
        .Take(page.PageSize)
        .Select(VisitProjection)
        .ToListAsync(ct);
        return new VisitPage(items, total);
    }

    public Task<VisitDto?> GetVisitDtoAsync(int id, CancellationToken ct = default) =>
        _db.Visits.AsNoTracking().Where(v => v.Id == id).Select(VisitProjection).FirstOrDefaultAsync(ct);

    public Task<bool> PatientExistsAsync(int patientId, CancellationToken ct = default) =>
        _db.Patients.AsNoTracking().AnyAsync(p => p.Id == patientId, ct);
        
    public Task<bool> HasOpenVisitAsync(int patientId, CancellationToken ct = default) =>
        _db.Visits.AsNoTracking().AnyAsync(v => v.MedicalRecord.PatientId == patientId && v.Status == VisitStatus.InProgress, ct);

    public Task<bool> IsDoctorOnDutyAsync(int doctorId, byte dayOfWeek, CancellationToken ct = default) =>
        _db.DoctorShifts.AsNoTracking().AnyAsync(s => s.DoctorId == doctorId && s.DayOfWeek == dayOfWeek && s.IsActive, ct);

    public Task<string?> GetPatientNameAsync(int patientId, CancellationToken ct = default) =>
        _db.Patients.AsNoTracking().Where(p => p.Id == patientId).Select(p => p.FullName).FirstOrDefaultAsync(ct);

    public Task<Visit?> GetVisitForUpdateAsync(int id, CancellationToken ct = default) =>
        _db.Visits.Include(v=>v.MedicalRecord).FirstOrDefaultAsync(v => v.Id == id, ct);


    public async Task<VisitCloseData> GetVisitCloseDataAsync(int visitId, CancellationToken ct = default)
    {
        var pending = await _db.FundusImages.AsNoTracking()
            .CountAsync(f => f.VisitId == visitId && f.QualityStatus == QualityStatus.Pending, ct);

        var withoutAi = await _db.FundusImages.AsNoTracking()
            .CountAsync(f => f.VisitId == visitId && f.QualityStatus == QualityStatus.Gradable
                && !_db.AiDiagnoses.Any(d => d.FundusImageId == f.Id), ct);

        var diagnoses = _db.AiDiagnoses.AsNoTracking()
            .Where(d => d.FundusImage != null && d.FundusImage.VisitId == visitId
                        && d.FundusImage.QualityStatus == QualityStatus.Gradable);

        var totalAi = await diagnoses.CountAsync(ct);
        var reviewed = await diagnoses.CountAsync(d => _db.DiagnosisReviews.Any(r => r.AiDiagnosisId == d.Id && !r.IsVoided), ct);

        var worst = await _db.DiagnosisReviews.AsNoTracking()
            .Where(r => !r.IsVoided && r.AiDiagnosis != null && r.AiDiagnosis.FundusImage != null
                        && r.AiDiagnosis.FundusImage.VisitId == visitId)
            .Select(r => (byte?)(byte)r.FinalGrade)
            .MaxAsync(ct);
        return new VisitCloseData(pending, withoutAi, totalAi, reviewed, worst);
    }

    public async Task<bool> VisitHasClinicalDataAsync(int visitId, CancellationToken ct = default)
    {
        // "Có dữ liệu" nghĩa là lượt khám đã phát sinh bất kỳ dữ liệu nghiệp vụ nào.
        // Worker không được tự đóng một lượt đã có kết luận/referral/recheck/ảnh/thuốc/feedback.
        var hasVisitFields = await _db.Visits.AsNoTracking().AnyAsync(v =>
            v.Id == visitId &&
            (v.Conclusion != null && v.Conclusion != ""
             || v.Referral != null
             || v.RecheckMonths != null), ct);

        if (hasVisitFields) return true;

        return await _db.FundusImages.AsNoTracking().AnyAsync(i => i.VisitId == visitId && !i.IsVoided, ct)
            || await _db.Prescriptions.AsNoTracking().AnyAsync(p => p.VisitId == visitId && !p.IsVoided, ct)
            || await _db.HealthMetrics.AsNoTracking().AnyAsync(m => m.VisitId == visitId, ct)
            || await _db.Feedbacks.AsNoTracking().AnyAsync(f => f.VisitId == visitId, ct);
    }

    public async Task<VisitPage> GetCompletedVisitsForPatientAsync(int patientId, PageQuery page, CancellationToken ct = default)
    {
        var query = _db.Visits.AsNoTracking().Where(v => v.MedicalRecord.PatientId == patientId && v.Status == VisitStatus.Completed);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(v => v.VisitDate).ThenByDescending(v => v.Id)
            .Skip(page.Skip).Take(page.PageSize).Select(VisitProjection).ToListAsync(ct);
        return new VisitPage(items, total);
    }

    public Task<VisitDto?> GetCompletedVisitForPatientAsync(int patientId, int visitId, CancellationToken ct = default) =>
        _db.Visits.AsNoTracking()
            .Where(v => v.Id == visitId && v.MedicalRecord.PatientId == patientId && v.Status == VisitStatus.Completed)
            .Select(VisitProjection).FirstOrDefaultAsync(ct);


    public async Task<IReadOnlyList<HealthMetric>> GetVisitHealthMetricsAsync(
        int visitId, bool tracking = false, CancellationToken ct = default)
    {
        IQueryable<HealthMetric> query = _db.HealthMetrics
            .Where(m => m.VisitId == visitId);

        if (!tracking) query = query.AsNoTracking();

        return await query
            .OrderBy(m => m.MetricType)
            .ThenBy(m => m.Id)
            .ToListAsync(ct);
    }

    private static readonly System.Linq.Expressions.Expression<Func<Visit, VisitDto>> VisitProjection = v => new VisitDto
    {
        Id = v.Id,
        PatientId = v.MedicalRecord.PatientId,
        PatientName = v.MedicalRecord.Patient!.FullName,
        PatientCode = v.MedicalRecord.Patient.Code,
        DoctorId = v.DoctorId,
        DoctorName = v.Doctor != null ? v.Doctor.FullName : null,
        VisitDate = v.VisitDate,
        Status = (byte)v.Status,
        Conclusion = v.Conclusion,
        Referral = (byte?)v.Referral,
        RecheckMonths = v.RecheckMonths,
        ClosedAt = v.ClosedAt,
        CreatedAt = v.CreatedAt,
        RowVersion = Convert.ToBase64String(v.RowVer),
        ImageCount = v.Images.Count(),
        PendingReviewCount = v.Images.SelectMany(i => i.Diagnoses).Count(d => !d.Reviews.Any(r => !r.IsVoided))
    };
}
