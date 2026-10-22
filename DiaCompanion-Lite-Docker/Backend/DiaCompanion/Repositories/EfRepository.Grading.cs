using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public Task<BlindGrade?> GetBlindGradeAsync(int imageId, byte round, CancellationToken ct = default) =>
        _db.BlindGrades.AsNoTracking()
            .FirstOrDefaultAsync(g => g.FundusImageId == imageId && g.Round == round, ct);

    public async Task<IReadOnlyList<BlindGrade>> GetBlindGradesForImageAsync(int imageId, CancellationToken ct = default) =>
        await _db.BlindGrades.AsNoTracking()
            .Where(g => g.FundusImageId == imageId)
            .OrderBy(g => g.Round)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BlindGrade>> GetBlindGradesForImagesAsync(
        IReadOnlyCollection<int> imageIds, CancellationToken ct = default)
    {
        if (imageIds.Count == 0) return Array.Empty<BlindGrade>();
        return await _db.BlindGrades.AsNoTracking()
            .Include(g => g.SourcePackage)   // 1.3.1: cột r2_source/r3_source của grading.csv
            .Where(g => imageIds.Contains(g.FundusImageId))
            .OrderBy(g => g.FundusImageId).ThenBy(g => g.Round)
            .ToListAsync(ct);
    }

    public Task<bool> ImageHasAiDiagnosisAsync(int imageId, CancellationToken ct = default) =>
        _db.AiDiagnoses.IgnoreQueryFilters().AsNoTracking().AnyAsync(d => d.FundusImageId == imageId, ct);

    public async Task<IReadOnlyList<GradingCandidate>> GetRound2CandidatesAsync(int graderId, CancellationToken ct = default)
    {
        // FundusImages có query filter loại ảnh đã thu hồi; join qua đó để bỏ vòng 1 của ảnh đã thu hồi.
        return await (
            from g in _db.BlindGrades.AsNoTracking()
            join f in _db.FundusImages.AsNoTracking() on g.FundusImageId equals f.Id
            where g.Round == 1 && g.Gradable
                  && g.GraderId != graderId
                  && !_db.BlindGrades.Any(x => x.FundusImageId == g.FundusImageId && (x.Round == 2 || x.GraderId == graderId))
            select new GradingCandidate(f.Id, f.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<(GradingCandidate Image, BlindGrade R1, BlindGrade R2)>> GetRound3PoolAsync(
        int graderId, CancellationToken ct = default)
    {
        var rows = await (
            from r1 in _db.BlindGrades.AsNoTracking()
            join r2 in _db.BlindGrades.AsNoTracking() on r1.FundusImageId equals r2.FundusImageId
            join f in _db.FundusImages.AsNoTracking() on r1.FundusImageId equals f.Id
            where r1.Round == 1 && r2.Round == 2
                  && r1.GraderId != graderId && r2.GraderId != graderId
                  && !_db.BlindGrades.Any(x => x.FundusImageId == r1.FundusImageId && x.Round == 3)
            select new { f.Id, f.CreatedAt, R1 = r1, R2 = r2 })
            .ToListAsync(ct);
        return rows.Select(x => (new GradingCandidate(x.Id, x.CreatedAt), x.R1, x.R2)).ToList();
    }

    public async Task<IReadOnlyList<GradingExportRow>> GetGradingExportRowsAsync(CancellationToken ct = default)
    {
        // Mọi ảnh còn hiệu lực thuộc một lượt khám, kể cả ảnh không chạy AI (không chấm được).
        var images = await _db.FundusImages.AsNoTracking()
            .Where(f => f.VisitId != null)
            .Select(f => new
            {
                f.Id,
                f.VisitId,
                f.Eye,
                f.QualityStatus,
                f.CreatedAt,
                f.FileSha256,
                PatientCode = f.Patient != null ? f.Patient.Code : null,
                HasAi = _db.AiDiagnoses.Any(d => d.FundusImageId == f.Id)
            })
            .OrderBy(f => f.Id)
            .ToListAsync(ct);

        var ids = images.Select(i => i.Id).ToList();
        var grades = (await GetBlindGradesForImagesAsync(ids, ct))
            .GroupBy(g => g.FundusImageId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<BlindGrade>)g.ToList());

        return images.Select(i => new GradingExportRow(
            i.Id, i.PatientCode, i.VisitId, i.Eye, i.QualityStatus, i.CreatedAt, i.HasAi,
            grades.TryGetValue(i.Id, out var list) ? list : Array.Empty<BlindGrade>(),
            i.FileSha256))
            .ToList();
    }

    public async Task<ImageClinicalContext?> GetImageClinicalContextAsync(int imageId, CancellationToken ct = default)
    {
        var image = await _db.FundusImages.AsNoTracking().FirstOrDefaultAsync(f => f.Id == imageId, ct);
        if (image is null) return null;
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == image.PatientId, ct);
        if (patient is null) return null;

        DateTime? visitDate = null;
        IReadOnlyList<HealthMetric> metrics = Array.Empty<HealthMetric>();
        if (image.VisitId is int visitId)
        {
            visitDate = await _db.Visits.AsNoTracking()
                .Where(v => v.Id == visitId).Select(v => (DateTime?)v.VisitDate).FirstOrDefaultAsync(ct);
            metrics = await _db.HealthMetrics.AsNoTracking()
                .Where(m => m.VisitId == visitId)
                .OrderByDescending(m => m.RecordedAtUtc).ThenByDescending(m => m.Id)
                .ToListAsync(ct);
        }
        return new ImageClinicalContext(image, patient, visitDate, metrics);
    }
}
