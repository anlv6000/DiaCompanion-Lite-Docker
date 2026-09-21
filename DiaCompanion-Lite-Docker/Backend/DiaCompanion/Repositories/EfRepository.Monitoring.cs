using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public async Task<HealthMetricPage> GetHealthMetricPageAsync(
        int patientId,
        MetricType? type,
        DateOnly? from,
        DateOnly? to,
        DateTime? cursorAt,
        long? cursorId,
        int size,
        CancellationToken ct = default)
    {
        var query = _db.HealthMetrics.AsNoTracking().Where(m => m.PatientId == patientId);
        if (type is MetricType metricType)
            query = query.Where(m => m.MetricType == metricType);
        if (from is DateOnly fromDate)
            query = query.Where(m => m.RecordedLocalDate >= fromDate);
        if (to is DateOnly toDate)
            query = query.Where(m => m.RecordedLocalDate <= toDate);
        if (cursorAt is DateTime at && cursorId is long id)
            query = query.Where(m => m.RecordedAtUtc < at || (m.RecordedAtUtc == at && m.Id < id));

        var rows = await query
            .OrderByDescending(m => m.RecordedAtUtc)
            .ThenByDescending(m => m.Id)
            .Take(size + 1)
            .ToListAsync(ct);

        var hasMore = rows.Count > size;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);

        var bpTimes = rows
            .Where(m => m.MetricType is MetricType.SystolicBp or MetricType.DiastolicBp)
            .Select(m => m.RecordedAtUtc)
            .Distinct()
            .ToArray();

        IReadOnlyList<HealthMetric> bpRows = bpTimes.Length == 0
            ? Array.Empty<HealthMetric>()
            : await _db.HealthMetrics.AsNoTracking()
                .Where(m => m.PatientId == patientId
                            && bpTimes.Contains(m.RecordedAtUtc)
                            && (m.MetricType == MetricType.SystolicBp
                                || m.MetricType == MetricType.DiastolicBp))
                .ToListAsync(ct);

        return new HealthMetricPage(rows, bpRows, hasMore);
    }

    public Task<HealthMetric?> GetHealthMetricForUpdateAsync(int id, CancellationToken ct = default) =>
        _db.HealthMetrics.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<HealthMetric?> GetBloodPressurePairForUpdateAsync(
        int patientId, int? visitId, DateTime recordedAtUtc, MetricType pairType, CancellationToken ct = default) =>
        _db.HealthMetrics.FirstOrDefaultAsync(x =>
            x.PatientId == patientId &&
            x.VisitId == visitId &&
            x.RecordedAtUtc == recordedAtUtc &&
            x.MetricType == pairType, ct);

    public async Task<IReadOnlyList<HealthMetric>> GetHealthMetricsAsync(
        int patientId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await _db.HealthMetrics.AsNoTracking()
            .Where(m => m.PatientId == patientId
                        && m.RecordedLocalDate >= from
                        && m.RecordedLocalDate <= to)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LifestyleLog>> GetLifestyleLogsAsync(
        int patientId, DateOnly from, CancellationToken ct = default) =>
        await _db.LifestyleLogs.AsNoTracking()
            .Where(l => l.PatientId == patientId && l.LogLocalDate >= from)
            .OrderByDescending(l => l.LogLocalDate)
            .ToListAsync(ct);

    public Task<LifestyleLog?> GetLifestyleLogForUpdateAsync(
        int id, int? patientId = null, CancellationToken ct = default)
    {
        var query = _db.LifestyleLogs.Where(l => l.Id == id);
        if (patientId is int pid)
            query = query.Where(l => l.PatientId == pid);
        return query.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<MedicationLog>> GetMedicationLogsForDateAsync(
        int patientId, DateOnly date, CancellationToken ct = default) =>
        await _db.MedicationLogs.AsNoTracking()
            .Include(m => m.PrescriptionItem)
            .Where(m => m.PatientId == patientId
                        && m.ScheduledLocalDate == date
                        && m.Status != MedicationStatus.Cancelled)
            .OrderBy(m => m.ScheduledAt)
            .ToListAsync(ct);

    public Task<MedicationLog?> GetMedicationLogForUpdateAsync(int id, CancellationToken ct = default) =>
        _db.MedicationLogs
            .Include(m => m.PrescriptionItem)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
}
