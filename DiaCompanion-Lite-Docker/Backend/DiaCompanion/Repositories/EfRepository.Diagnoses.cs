using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public Task<bool> IsImageReviewedAsync(int imageId, CancellationToken ct = default) =>
        _db.DiagnosisReviews.AsNoTracking()
            .AnyAsync(r => r.AiDiagnosis != null && r.AiDiagnosis.FundusImageId == imageId, ct);

    public async Task<IReadOnlyList<ModelVersion>> GetActiveModelVersionsAsync(CancellationToken ct = default) =>
        await _db.ModelVersions.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.ModelType)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AiDiagnosis>> GetDiagnosesForImageForUpdateAsync(int imageId, CancellationToken ct = default) =>
        await _db.AiDiagnoses.Where(d => d.FundusImageId == imageId)
            .OrderBy(d => d.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<int>> GetDiagnosisIdsByImageAsync(int imageId, CancellationToken ct = default) =>
        await _db.AiDiagnoses.AsNoTracking().Where(d => d.FundusImageId == imageId)
            .OrderByDescending(d => d.CreatedAt).Select(d => d.Id).ToListAsync(ct);

    public Task<AiDiagnosis?> GetDiagnosisWithImageAsync(int id, CancellationToken ct = default) =>
        _db.AiDiagnoses.AsNoTracking().Include(d => d.FundusImage)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<ConfirmedProgressionRow>> GetConfirmedProgressionAsync(
        int patientId, DateTime from, CancellationToken ct = default) =>
        await _db.DiagnosisReviews.AsNoTracking()
            .Where(r => r.AiDiagnosis!.FundusImage!.PatientId == patientId && r.CreatedAt >= from)
            .Select(r => new ConfirmedProgressionRow(
                r.CreatedAt,
                r.AiDiagnosis!.FundusImage!.VisitId,
                r.FinalGrade,
                r.AiDiagnosis.FractalDimension))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Hba1cProgressionRow>> GetHba1cProgressionAsync(
        int patientId, DateTime from, CancellationToken ct = default) =>
        await _db.HealthMetrics.AsNoTracking()
            .Where(m => m.PatientId == patientId && m.MetricType == MetricType.HbA1c && m.RecordedAtUtc >= from)
            .Select(m => new Hba1cProgressionRow(m.RecordedAtUtc, m.Value))
            .ToListAsync(ct);

    public Task<AiDiagnosis?> GetDiagnosisDetailAsync(int id, CancellationToken ct = default) =>
        _db.AiDiagnoses.AsNoTracking()
            .Include(x => x.FundusImage)
                .ThenInclude(x => x!.Visit)
            .Include(x => x.ModelVersion)
            .Include(x => x.LesionModelVersion)
            .Include(x => x.FractalModelVersion)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<DiagnosisReview?> GetReviewByDiagnosisAsync(int diagnosisId, CancellationToken ct = default) =>
        _db.DiagnosisReviews.AsNoTracking().Include(r => r.Doctor)
            .FirstOrDefaultAsync(r => r.AiDiagnosisId == diagnosisId, ct);
}
