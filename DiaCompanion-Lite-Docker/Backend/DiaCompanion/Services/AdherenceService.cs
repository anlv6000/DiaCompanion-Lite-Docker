using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Repositories;

namespace DiaCompanion.Api.Services;

/// <summary>NF-10 / NF-12: sinh lịch nhắc thuốc và tính tỉ lệ tuân thủ.</summary>
public interface IAdherenceService
{
    void GenerateSchedule(Prescription prescription, IEnumerable<PrescriptionItem> items);
    Task<AdherenceSummary> GetAsync(
        int patientId,
        int days = 30,
        int? prescriptionId = null,
        DateOnly? from = null,
        DateOnly? to = null);
}

public record AdherenceSummary(
    int Total,
    int Taken,
    int Missed,
    int Skipped,
    int Pending,
    decimal Rate);

public class AdherenceService : IAdherenceService
{
    private readonly IRepository _repository;
    private readonly IClinicClock _clock;

    public AdherenceService(IRepository repository, IClinicClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    private static readonly Dictionary<byte, int[]> DoseHours = new()
    {
        [1] = new[] { 8 },
        [2] = new[] { 8, 20 },
        [3] = new[] { 8, 12, 20 },
        [4] = new[] { 7, 11, 15, 20 },
        [5] = new[] { 7, 10, 13, 17, 21 },
        [6] = new[] { 6, 9, 12, 15, 18, 21 }
    };

    public void GenerateSchedule(Prescription prescription, IEnumerable<PrescriptionItem> items)
    {
        var startLocal = _clock.LocalNow.Date;

        foreach (var item in items.Where(i => i.IsActive))
        {
            var hours = DoseHours.TryGetValue(item.TimesPerDay, out var configured)
                ? configured
                : new[] { 8 };

            for (var day = 0; day < item.DurationDays; day++)
            {
                var localDate = startLocal.AddDays(day);
                foreach (var hour in hours)
                {
                    var localDateTime = localDate.AddHours(hour);
                    _repository.Add(new MedicationLog
                    {
                        PatientId = prescription.PatientId,
                        PrescriptionItemId = item.Id,
                        ScheduledAt = _clock.ToUtc(localDateTime),
                        ScheduledLocalDate = DateOnly.FromDateTime(localDate),
                        Status = MedicationStatus.Pending
                    });
                }
            }
        }
    }

    public async Task<AdherenceSummary> GetAsync(
        int patientId,
        int days = 30,
        int? prescriptionId = null,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        if (days is < 1 or > 3650)
            throw AppException.BadRequest(Msg.InvalidData, "Số ngày phải nằm trong khoảng 1–3650.");

        var toDate = to ?? _clock.LocalToday;
        var fromDate = from ?? toDate.AddDays(-(days - 1));
        if (fromDate > toDate)
            throw AppException.BadRequest(Msg.InvalidData, "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");

        var statuses = await _repository.GetMedicationStatusesAsync(
            patientId, fromDate, toDate, prescriptionId);
        var taken = statuses.Count(s => s == MedicationStatus.Taken);
        var missed = statuses.Count(s => s == MedicationStatus.Missed);
        var skipped = statuses.Count(s => s == MedicationStatus.Skipped);
        var pending = statuses.Count(s => s == MedicationStatus.Pending);
        var due = taken + missed + skipped;
        var rate = due == 0 ? 0m : Math.Round(taken * 100m / due, 1);

        return new AdherenceSummary(statuses.Count, taken, missed, skipped, pending, rate);
    }
}
