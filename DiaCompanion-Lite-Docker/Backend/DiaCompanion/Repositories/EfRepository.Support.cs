using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public Task<string?> GetSystemConfigValueAsync(string key, CancellationToken ct = default) =>
        _db.SystemConfigs.AsNoTracking().Where(c => c.Key == key).Select(c => c.Value).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<OtpCode>> GetUnconsumedOtpCodesAsync(
        string phone, OtpPurpose purpose, CancellationToken ct = default) =>
        await _db.OtpCodes
            .Where(o => o.Phone == phone && o.Purpose == purpose && o.ConsumedAt == null)
            .ToListAsync(ct);

    public Task<OtpCode?> GetLatestUnconsumedOtpAsync(
        string phone, OtpPurpose purpose, CancellationToken ct = default) =>
        _db.OtpCodes
            .Where(o => o.Phone == phone && o.Purpose == purpose && o.ConsumedAt == null)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<MedicationStatus>> GetMedicationStatusesAsync(
        int patientId, DateOnly from, DateOnly to, int? prescriptionId = null, CancellationToken ct = default)
    {
        var query = _db.MedicationLogs.AsNoTracking()
            .Where(m => m.PatientId == patientId
                        && m.ScheduledLocalDate >= from
                        && m.ScheduledLocalDate <= to
                        && m.Status != MedicationStatus.Cancelled);
        if (prescriptionId is int pid)
            query = query.Where(m => m.PrescriptionItem!.PrescriptionId == pid);
        return await query.Select(m => m.Status).ToListAsync(ct);
    }


    public async Task InvalidateUnconsumedOtpCodesForPhoneAsync(
    string phone,
    CancellationToken ct = default)
    {
        var rows = await _db.OtpCodes
            .Where(o =>
                o.Phone == phone &&
                o.ConsumedAt == null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var row in rows)
        {
            row.ConsumedAt = now;
        }
    }
}
