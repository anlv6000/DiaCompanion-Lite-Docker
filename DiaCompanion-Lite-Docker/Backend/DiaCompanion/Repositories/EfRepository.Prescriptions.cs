using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public async Task<PrescriptionPage> GetPrescriptionPageAsync(
        int patientId,
        string? keyword,
        DateTime? fromUtc,
        DateTime? toExclusiveUtc,
        bool? voided,
        PageQuery page,
        CancellationToken ct = default)
    {
        var query = _db.Prescriptions.AsNoTracking()
            .Where(p => p.PatientId == patientId);

        if (voided is bool isVoided) query = query.Where(p => p.IsVoided == isVoided);
        if (fromUtc is DateTime from) query = query.Where(p => p.IssuedAt >= from);
        if (toExclusiveUtc is DateTime to) query = query.Where(p => p.IssuedAt < to);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(p =>
                (p.Note != null && EF.Functions.Like(p.Note, $"%{term}%")) ||
                p.Items.Any(i => EF.Functions.Like(i.DrugName, $"%{term}%")));
        }

        var total = await query.CountAsync(ct);
        query = page.Sort?.Trim().ToLowerInvariant() switch
        {
            "doctor" => page.Desc
                ? query.OrderByDescending(p => p.Doctor!.FullName).ThenByDescending(p => p.Id)
                : query.OrderBy(p => p.Doctor!.FullName).ThenBy(p => p.Id),
            "status" => page.Desc
                ? query.OrderByDescending(p => p.IsVoided).ThenByDescending(p => p.IssuedAt)
                : query.OrderBy(p => p.IsVoided).ThenByDescending(p => p.IssuedAt),
            _ => page.Desc
                ? query.OrderBy(p => p.IssuedAt).ThenBy(p => p.Id)
                : query.OrderByDescending(p => p.IssuedAt).ThenByDescending(p => p.Id)
        };

        var rows = await query.Include(p => p.Doctor).Include(p => p.Items).AsSplitQuery()
            .Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PrescriptionPage(rows, total);
    }

    public Task<Prescription?> GetPrescriptionAsync(int id, bool tracking, CancellationToken ct = default)
    {
        IQueryable<Prescription> query = _db.Prescriptions.IgnoreQueryFilters()
            .Include(p => p.Doctor)
            .Include(p => p.Items)
            .Where(p => p.Id == id);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<MedicationLog>> GetPendingMedicationLogsForItemsAsync(
        IEnumerable<int> prescriptionItemIds, CancellationToken ct = default)
    {
        var ids = prescriptionItemIds.Distinct().ToArray();
        if (ids.Length == 0) return Array.Empty<MedicationLog>();
        return await _db.MedicationLogs
            .Where(m => ids.Contains(m.PrescriptionItemId) && m.Status == MedicationStatus.Pending)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, PrescriptionMedicationStats>> GetPrescriptionMedicationStatsAsync(
        IEnumerable<int> prescriptionIds, CancellationToken ct = default)
    {
        var ids = prescriptionIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, PrescriptionMedicationStats>();

        var rows = await _db.MedicationLogs.AsNoTracking()
            .Where(l => ids.Contains(l.PrescriptionItem!.PrescriptionId) && l.Status != MedicationStatus.Cancelled)
            .GroupBy(l => l.PrescriptionItem!.PrescriptionId)
            .Select(g => new
            {
                PrescriptionId = g.Key,
                Total = g.Count(),
                Taken = g.Count(x => x.Status == MedicationStatus.Taken),
                Missed = g.Count(x => x.Status == MedicationStatus.Missed),
                Skipped = g.Count(x => x.Status == MedicationStatus.Skipped)
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            x => x.PrescriptionId,
            x => new PrescriptionMedicationStats(x.Total, x.Taken, x.Missed, x.Skipped));
    }
}
