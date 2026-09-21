using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public async Task<DashboardStats> GetDashboardStatsAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        int? modelVersionId,
        int? doctorId,
        bool countAllPatients,
        CancellationToken ct = default)
    {
        var diagnoses = _db.AiDiagnoses.AsNoTracking()
            .Where(d => !d.IsVoided && d.CreatedAt >= fromUtc && d.CreatedAt < toExclusiveUtc);
        var reviews = _db.DiagnosisReviews.AsNoTracking()
            .Where(r => !r.IsVoided && r.CreatedAt >= fromUtc && r.CreatedAt < toExclusiveUtc);
        var visits = _db.Visits.AsNoTracking()
            .Where(v => !v.IsVoided && v.VisitDate >= fromUtc && v.VisitDate < toExclusiveUtc);

        if (doctorId is int did)
        {
            diagnoses = diagnoses.Where(d => d.FundusImage!.Visit != null && d.FundusImage.Visit.DoctorId == did);
            reviews = reviews.Where(r => r.AiDiagnosis!.FundusImage!.Visit != null && r.AiDiagnosis.FundusImage.Visit.DoctorId == did);
            visits = visits.Where(v => v.DoctorId == did);
        }

        if (modelVersionId is int mid)
        {
            diagnoses = diagnoses.Where(d => d.ModelVersionId == mid
                || d.LesionModelVersionId == mid
                || d.FractalModelVersionId == mid);
            reviews = reviews.Where(r => r.AiDiagnosis!.ModelVersionId == mid
                || r.AiDiagnosis.LesionModelVersionId == mid
                || r.AiDiagnosis.FractalModelVersionId == mid);

            // Khi dashboard lọc theo một version bất kỳ trong bộ 3 model,
            // các KPI theo lượt khám/bệnh nhân cũng phải cùng phạm vi. Nếu không,
            // deferral/override được lọc theo model nhưng visit/referral/patient lại
            // vẫn là toàn bộ kỳ -> biểu đồ và KPI không cùng mẫu số.
            visits = visits.Where(v => v.Images.Any(i => i.Diagnoses.Any(d =>
                d.ModelVersionId == mid
                || d.LesionModelVersionId == mid
                || d.FractalModelVersionId == mid)));
        }

        var totalDiagnoses = await diagnoses.CountAsync(ct);
        var deferredTotal = await diagnoses.CountAsync(d => d.IsDeferred, ct);
        var totalReviews = await reviews.CountAsync(ct);
        var overrides = await reviews.CountAsync(r => r.Action == ReviewAction.Override, ct);
        var closedVisits = await visits.CountAsync(v => v.Status == VisitStatus.Completed, ct);
        var referred = await visits.CountAsync(v => v.Status == VisitStatus.Completed &&
            v.Referral.HasValue && v.Referral.Value >= ReferralType.Ophthalmology, ct);
        var distribution = await reviews.GroupBy(r => r.FinalGrade)
            .Select(g => new GradeCount((byte)g.Key, g.Count())).ToListAsync(ct);

        var patientCount = countAllPatients
            ? await _db.Patients.CountAsync(ct)
            : await visits.Select(v => v.MedicalRecord.PatientId).Distinct().CountAsync(ct);

        var visitCount = await visits.CountAsync(ct);
        var pendingTriage = await diagnoses.CountAsync(d => !d.Reviews.Any(r => !r.IsVoided), ct);
        var deferredPending = await diagnoses.CountAsync(d => !d.Reviews.Any(r => !r.IsVoided) && d.IsDeferred, ct);
        var activeModels = await _db.ModelVersions.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.ModelType)
            .Select(m => new { m.ModelType, m.Name })
            .ToListAsync(ct);
        var activeModel = activeModels.Count == 0 ? null : string.Join(" | ",
            activeModels.Select(m => $"{m.ModelType}: {m.Name}"));

        return new DashboardStats(
            totalDiagnoses, deferredTotal, totalReviews, overrides, closedVisits, referred,
            patientCount, visitCount, pendingTriage, deferredPending, distribution, activeModel);
    }

    public async Task<IReadOnlyList<SystemConfig>> GetSystemConfigsAsync(CancellationToken ct = default) =>
        await _db.SystemConfigs.AsNoTracking().OrderBy(c => c.Key).ToListAsync(ct);

    public Task<SystemConfig?> GetSystemConfigForUpdateAsync(string key, CancellationToken ct = default) =>
        _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key, ct);

    public async Task<IReadOnlyList<DiagnosisThresholdRow>> GetDiagnosisThresholdRowsAsync(CancellationToken ct = default) =>
        await _db.AiDiagnoses.AsNoTracking()
            .Select(d => new DiagnosisThresholdRow(d.Disagreement, d.IsDeferred))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ModelVersionWithCount>> GetModelVersionsWithCountsAsync(CancellationToken ct = default)
    {
        var models = await _db.ModelVersions.AsNoTracking()
            .OrderBy(m => m.ModelType)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
        if (models.Count == 0) return Array.Empty<ModelVersionWithCount>();

        var refs = await _db.AiDiagnoses.AsNoTracking()
            .Select(d => new { d.Id, Dr = d.ModelVersionId, d.LesionModelVersionId, d.FractalModelVersionId })
            .ToListAsync(ct);

        return models.Select(m => new ModelVersionWithCount(
            m,
            refs.Count(d => d.Dr == m.Id || d.LesionModelVersionId == m.Id || d.FractalModelVersionId == m.Id)))
            .ToList();
    }

    public Task<ModelVersion?> GetModelVersionAsync(int id, bool tracking, CancellationToken ct = default)
    {
        IQueryable<ModelVersion> query = _db.ModelVersions.Where(m => m.Id == id);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(ct);
    }

    public Task<bool> ModelNameExistsAsync(string name, CancellationToken ct = default) =>
        _db.ModelVersions.AnyAsync(m => m.Name == name, ct);

    public async Task<IReadOnlyList<ModelVersion>> GetOtherActiveModelsForUpdateAsync(
        int excludedModelId, ModelType modelType, CancellationToken ct = default) =>
        await _db.ModelVersions
            .Where(m => m.IsActive && m.ModelType == modelType && m.Id != excludedModelId)
            .ToListAsync(ct);

    public Task<bool> ModelHasDiagnosesAsync(int modelVersionId, CancellationToken ct = default) =>
        _db.AiDiagnoses.AnyAsync(d => d.ModelVersionId == modelVersionId
            || d.LesionModelVersionId == modelVersionId
            || d.FractalModelVersionId == modelVersionId, ct);

    public Task<int> CountDiagnosesForModelAsync(int modelVersionId, CancellationToken ct = default) =>
        _db.AiDiagnoses.AsNoTracking().CountAsync(d => d.ModelVersionId == modelVersionId
            || d.LesionModelVersionId == modelVersionId
            || d.FractalModelVersionId == modelVersionId, ct);

    public async Task<AuditPage> GetAuditPageAsync(
        string? action,
        string? entityType,
        int? entityId,
        int? userId,
        DateTime? from,
        DateTime? to,
        DateTime? cursorAt,
        long? cursorId,
        int size,
        CancellationToken ct = default)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);
        if (entityId is int eid) query = query.Where(a => a.EntityId == eid);
        if (userId is int uid) query = query.Where(a => a.UserId == uid);
        if (from is DateTime f) query = query.Where(a => a.CreatedAt >= f);
        if (to is DateTime t) query = query.Where(a => a.CreatedAt <= t);
        if (cursorAt is DateTime at && cursorId is long lastId)
            query = query.Where(a => a.CreatedAt < at || (a.CreatedAt == at && a.Id < lastId));

        var rows = await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Take(size + 1).ToListAsync(ct);
        var hasMore = rows.Count > size;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        return new AuditPage(rows, hasMore);
    }
}
