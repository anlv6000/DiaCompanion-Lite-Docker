using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public Task<Visit?> GetVisitForExportAsync(int visitId, CancellationToken ct = default) =>
        _db.Visits.AsNoTracking()
            .Include(v => v.MedicalRecord.Patient)
            .Include(v => v.Doctor)
            .FirstOrDefaultAsync(v => v.Id == visitId, ct);

    public async Task<IReadOnlyList<DiagnosisReview>> GetVisitDiagnosisReviewsForExportAsync(
        int visitId, CancellationToken ct = default) =>
        await _db.DiagnosisReviews.AsNoTracking()
            .Include(r => r.Doctor)
            .Include(r => r.AiDiagnosis)!.ThenInclude(d => d.ModelVersion)
            .Include(r => r.AiDiagnosis)!.ThenInclude(d => d.LesionModelVersion)
            .Include(r => r.AiDiagnosis)!.ThenInclude(d => d.FractalModelVersion)
            .Include(r => r.AiDiagnosis)!.ThenInclude(d => d.FundusImage)!.ThenInclude(f => f.Patient)
            .Where(r => r.AiDiagnosis!.FundusImage!.VisitId == visitId)
            .OrderBy(r => r.AiDiagnosis!.FundusImage!.Eye)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Prescription>> GetVisitPrescriptionsForExportAsync(
        int visitId, CancellationToken ct = default) =>
        await _db.Prescriptions.AsNoTracking()
            .Include(p => p.Items)
            .Where(p => p.VisitId == visitId)
            .OrderBy(p => p.IssuedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<HealthMetric>> GetVisitHealthMetricsForExportAsync(
        int visitId, CancellationToken ct = default) =>
        await _db.HealthMetrics.AsNoTracking()
            .Where(m => m.VisitId == visitId)
            .OrderBy(m => m.MetricType)
            .ThenBy(m => m.RecordedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DiagnosisReview>> GetDiagnosisReviewsForExportAsync(
        int? modelVersionId,
        DateTime? fromUtc,
        DateTime? toExclusiveUtc,
        bool overridesOnly,
        CancellationToken ct = default)
    {
        var query = _db.DiagnosisReviews.AsNoTracking()
            .Include(r => r.Doctor)
            .Include(r => r.AiDiagnosis)!.ThenInclude(d => d.ModelVersion)
            .Include(r => r.AiDiagnosis)!.ThenInclude(d => d.LesionModelVersion)
            .Include(r => r.AiDiagnosis)!.ThenInclude(d => d.FractalModelVersion)
            .Include(r => r.AiDiagnosis)!.ThenInclude(d => d.FundusImage)!.ThenInclude(f => f.Patient)
            .AsQueryable();

        if (overridesOnly)
            query = query.Where(r => r.Action == ReviewAction.Override);
        if (modelVersionId is int mv)
            query = query.Where(r => r.AiDiagnosis!.ModelVersionId == mv
                || r.AiDiagnosis.LesionModelVersionId == mv
                || r.AiDiagnosis.FractalModelVersionId == mv);
        if (fromUtc is DateTime from)
            query = query.Where(r => r.CreatedAt >= from);
        if (toExclusiveUtc is DateTime to)
            query = query.Where(r => r.CreatedAt < to);

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AiDiagnosis>> GetResearchDatasetAsync(
        DateTime? fromUtc,
        DateTime? toExclusiveUtc,
        CancellationToken ct = default)
    {
        // Global query filter đã loại các bản ghi void (AiDiagnosis, DiagnosisReview,
        // MedicalRecord). Chỉ nạp thêm các nhánh cần cho một dòng dữ liệu nghiên cứu.
        var query = _db.AiDiagnoses.AsNoTracking()
            .Include(d => d.ModelVersion)
            .Include(d => d.LesionModelVersion)
            .Include(d => d.FractalModelVersion)
            .Include(d => d.Reviews).ThenInclude(r => r.Doctor)
            .Include(d => d.FundusImage)!.ThenInclude(f => f.Visit)!.ThenInclude(v => v!.MedicalRecord)!.ThenInclude(mr => mr.Patient)
            .AsQueryable();

        if (fromUtc is DateTime f) query = query.Where(d => d.CreatedAt >= f);
        if (toExclusiveUtc is DateTime t) query = query.Where(d => d.CreatedAt < t);

        return await query
            .OrderBy(d => d.FundusImage!.Visit!.MedicalRecord.PatientId)
            .ThenBy(d => d.FundusImage!.VisitId)
            .ThenBy(d => d.FundusImage!.Eye)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<HealthMetric>> GetHealthMetricsForVisitsAsync(
        IReadOnlyCollection<int> visitIds,
        CancellationToken ct = default)
    {
        if (visitIds.Count == 0) return System.Array.Empty<HealthMetric>();
        // Query filter đã loại metric đã xoá mềm.
        return await _db.HealthMetrics.AsNoTracking()
            .Where(m => m.VisitId != null && visitIds.Contains(m.VisitId.Value))
            .OrderBy(m => m.Id)
            .ToListAsync(ct);
    }
}
