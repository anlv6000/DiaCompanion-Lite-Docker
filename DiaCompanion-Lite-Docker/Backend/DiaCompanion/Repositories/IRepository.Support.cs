using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public partial interface IRepository
{
    Task<string?> GetSystemConfigValueAsync(string key, CancellationToken ct = default);

    Task<IReadOnlyList<OtpCode>> GetUnconsumedOtpCodesAsync(string phone, OtpPurpose purpose, CancellationToken ct = default);
    Task<OtpCode?> GetLatestUnconsumedOtpAsync(string phone, OtpPurpose purpose, CancellationToken ct = default);

    Task<IReadOnlyList<MedicationStatus>> GetMedicationStatusesAsync(
        int patientId, DateOnly from, DateOnly to, int? prescriptionId = null, CancellationToken ct = default);

    Task InvalidateUnconsumedOtpCodesForPhoneAsync(
    string phone,
    CancellationToken ct = default);
}
