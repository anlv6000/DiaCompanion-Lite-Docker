using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;
using System.ComponentModel;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public async Task<IReadOnlyList<OnDutyDoctorRow>> GetOnDutyDoctorsAsync(
        byte dayOfWeek, byte shift, DateTime startUtc, DateTime endUtc, string? q, CancellationToken ct = default)
    {
        // Lấy tất cả ca trực đang hoạt động và tương ứng với ca trực thực tế
        var shifts = _db.DoctorShifts.AsNoTracking()
            .Where(s => s.IsActive
                        && s.DayOfWeek == dayOfWeek
                        && (byte)s.Shift == shift);
        
        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            shifts = shifts.Where(s =>
                s.Doctor != null &&
                (s.Doctor.FullName.Contains(keyword)
                 || (s.Doctor.LicenseNo != null && s.Doctor.LicenseNo.Contains(keyword))));
        }
        var rows = await shifts
            .Where(s =>
                    s.Doctor != null &&
                    s.Doctor.UserRoles.Any(ur =>
                        ur.IsActive &&
                        ur.Role.IsActive &&
                        ur.Role.Name == Roles.Doctor))
            .Select(s => new
            {
                Shift = (byte)s.Shift,
                DoctorId = s.DoctorId,
                DoctorName = s.Doctor!.FullName,
                LicenseNo = s.Doctor.LicenseNo 
            }).ToListAsync(ct);
        // Với mỗi bác sĩ lấy ra số lượt khám đang mở
        var counts = await _db.Visits.AsNoTracking()
            .Where(v => v.Status == VisitStatus.InProgress && v.DoctorId != null
                        && v.VisitDate >= startUtc && v.VisitDate < endUtc)
            .GroupBy(v => v.DoctorId!.Value)
            .Select(g => new { DoctorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DoctorId, x => x.Count, ct);

        return rows.Select(r => new OnDutyDoctorRow(
            (byte)r.Shift, r.DoctorId, r.DoctorName, r.LicenseNo,
            counts.TryGetValue(r.DoctorId, out var count) ? count : 0)).ToList();
    }

    public async Task<IReadOnlyList<DoctorShiftRow>> GetDoctorShiftsAsync(int? doctorId, CancellationToken ct = default)
    {
        var query = _db.DoctorShifts.AsNoTracking().AsQueryable();
        if (doctorId is int id) query = query.Where(s => s.DoctorId == id);

        var rows = await query
            .Join(_db.Users.AsNoTracking(), s => s.DoctorId, u => u.Id, (s, u) => new { s, u })
            .OrderBy(x => x.s.DayOfWeek).ThenBy(x => x.s.Shift).ThenBy(x => x.u.FullName)
            .ToListAsync(ct);
        return rows.Select(x => new DoctorShiftRow(x.s, x.u)).ToList();
    }

    public async Task<DoctorShiftRow?> GetDoctorShiftRowAsync(int id, CancellationToken ct = default)
    {
        var row = await _db.DoctorShifts.AsNoTracking()
            .Where(s => s.Id == id)
            .Join(_db.Users.AsNoTracking(), s => s.DoctorId, u => u.Id, (s, u) => new { s, u })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : new DoctorShiftRow(row.s, row.u);
    }

    public Task<DoctorShift?> GetDoctorShiftForUpdateAsync(int id, CancellationToken ct = default) =>
        _db.DoctorShifts.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<byte>> GetExistingShiftDaysAsync(int doctorId, byte shift, CancellationToken ct = default) =>
        await _db.DoctorShifts.AsNoTracking()
            .Where(s => s.DoctorId == doctorId && (byte)s.Shift == shift)
            .Select(s => s.DayOfWeek)
            .ToListAsync(ct);

    public Task<bool> DoctorShiftExistsAsync(int doctorId, byte dayOfWeek, byte shift, CancellationToken ct = default) =>
        _db.DoctorShifts.AsNoTracking()
            .AnyAsync(s => s.DoctorId == doctorId && s.DayOfWeek == dayOfWeek && (byte)s.Shift == shift, ct);
}
