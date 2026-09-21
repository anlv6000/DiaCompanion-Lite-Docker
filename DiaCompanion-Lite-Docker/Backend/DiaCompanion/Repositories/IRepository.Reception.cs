using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record DoctorShiftRow(DoctorShift Shift, User Doctor);
public sealed record OnDutyDoctorRow(byte Shift, int DoctorId, string DoctorName, string? LicenseNo, int OpenVisitCount);

public partial interface IRepository
{
    Task<IReadOnlyList<OnDutyDoctorRow>> GetOnDutyDoctorsAsync(
        byte dayOfWeek, byte shift, DateTime dayStartUtc, DateTime dayEndUtc, string? q, CancellationToken ct = default);
    Task<IReadOnlyList<DoctorShiftRow>> GetDoctorShiftsAsync(int? doctorId, CancellationToken ct = default);
    Task<DoctorShiftRow?> GetDoctorShiftRowAsync(int id, CancellationToken ct = default);
    Task<DoctorShift?> GetDoctorShiftForUpdateAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<byte>> GetExistingShiftDaysAsync(int doctorId, byte shift, CancellationToken ct = default);
    Task<bool> DoctorShiftExistsAsync(int doctorId, byte dayOfWeek, byte shift, CancellationToken ct = default);
}
