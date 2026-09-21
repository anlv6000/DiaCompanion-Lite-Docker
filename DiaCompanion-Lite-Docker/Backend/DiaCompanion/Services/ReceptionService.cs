using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Repositories;
using System.Threading.Tasks;

namespace DiaCompanion.Api.Services;

public class ReceptionService : BaseService, IReceptionService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IClinicClock _clock;
    private readonly IConfigService _cfg;

    private static readonly TimeOnly MorningStart = new(7, 0);
    private static readonly TimeOnly AfternoonStart = new(14, 0);
    private static readonly TimeOnly NightStart = new(18, 0);
    public ReceptionService(IRepository repository, ICurrentUser me, IClinicClock clock, IConfigService cfg)
    { _repository = repository; _me = me; _clock = clock; _cfg = cfg; }

    public async Task<ActionResult<OnDutyResponse>> OnDuty(DateOnly? date, byte? shift, string? q)
    {
        // Lấy giờ ca trực trong config
        var configShiftTime = await GetShiftTimesAsync();
        // Lấy ca trực theo giờ ca trực trong config
        var currentShift = ResolveCurrentShift(configShiftTime);
        // Lấy ngày thực tế cần để query
        var currentDate = currentShift.ScheduleAt;
        var dow = (byte)currentDate.DayOfWeek;
        // Lấy giờ trực từ ngày thực tế, ca trực thực tế, theo giờ trực trong config
        var shiftRange = ResolveShiftUtcRange(currentDate, currentShift.Shift, configShiftTime);
        var rows = await _repository.GetOnDutyDoctorsAsync(dow, currentShift.Shift, shiftRange.StartUtc, shiftRange.EndUtc, q);

        var doctors = rows
            .GroupBy(r => new { r.DoctorId, r.DoctorName, r.LicenseNo, r.OpenVisitCount })
            .Select(g =>
            {
                return new OnDutyDoctorDto
                {
                    DoctorId = g.Key.DoctorId,
                    DoctorName = g.Key.DoctorName,
                    LicenseNo = g.Key.LicenseNo,
                    Shift = currentShift.Shift,
                    ShiftLabel = ShiftLabel(currentShift.Shift),
                    OpenVisitCount = g.Key.OpenVisitCount
                };
            })
            .OrderBy(x => x.OpenVisitCount).ThenBy(x => x.DoctorName).ToList();

        return Ok(new OnDutyResponse
        {
            Date = currentDate,
            DayLabel = DayLabel(dow),
            CurrentShift = currentShift.Shift,
            Doctors = doctors
        });
    }

    public async Task<ActionResult<List<DoctorShiftDto>>> ListShifts(int? doctorId)
    {
        var rows = await _repository.GetDoctorShiftsAsync(doctorId);
        return Ok(rows.Select(MapShift).ToList());
    }

    public async Task<ActionResult<DoctorShiftDto>> CreateShift(CreateDoctorShiftRequest req)
    {
        await EnsureDoctorAsync(req.DoctorId);
        await EnsureNoDuplicateAsync(req.DoctorId, req.DayOfWeek, req.Shift);

        var shift = new DoctorShift
        {
            DoctorId = req.DoctorId,
            DayOfWeek = req.DayOfWeek,
            Shift = (ShiftType)req.Shift,
            CreatedBy = _me.RequireId()
        };
        _repository.Add(shift);
        await _repository.CommitAsync();
        return Ok(await GetShiftDtoAsync(shift.Id));
    }

    public async Task<ActionResult<List<DoctorShiftDto>>> CreateShiftsBatch(CreateDoctorShiftsBatchRequest req)
    {
        await EnsureDoctorAsync(req.DoctorId);
        var existing = await _repository.GetExistingShiftDaysAsync(req.DoctorId, req.Shift);
        var existingSet = existing.ToHashSet();
        var rows = req.DaysOfWeek.Distinct()
            .Where(d => d <= 6 && !existingSet.Contains(d))
            .Select(d => new DoctorShift
            {
                DoctorId = req.DoctorId,
                DayOfWeek = d,
                Shift = (ShiftType)req.Shift,
                CreatedBy = _me.RequireId()
            }).ToList();
        if (rows.Count > 0)
        {
            _repository.AddRange(rows);
            await _repository.CommitAsync();
        }
        return await ListShifts(req.DoctorId);
    }

    public async Task<ActionResult<DoctorShiftDto>> SetShiftActive(int id, bool active, string rowVersion)
    {
        var shift = await _repository.GetDoctorShiftForUpdateAsync(id)
            ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy ca trực.");
        _repository.ApplyOriginalRowVersion(shift, rowVersion);
        shift.IsActive = active;
        shift.UpdatedAt = _clock.UtcNow;
        await _repository.CommitAsync();
        return Ok(await GetShiftDtoAsync(id));
    }

    public async Task<IActionResult> DeleteShift(int id, string rowVersion)
    {
        var shift = await _repository.GetDoctorShiftForUpdateAsync(id)
            ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy ca trực.");
        _repository.ApplyOriginalRowVersion(shift, rowVersion);
        _repository.Remove(shift);
        await _repository.CommitAsync();
        return NoContent();
    }

    private async Task EnsureDoctorAsync(int doctorId)
    {
        if (!await _repository.IsActiveUserInRoleAsync(doctorId, Roles.Doctor))
            throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy bác sĩ đang hoạt động.");
    }

    private async Task EnsureNoDuplicateAsync(int doctorId, byte dow, byte shift)
    {
        if (await _repository.DoctorShiftExistsAsync(doctorId, dow, shift))
            throw AppException.Conflict(Msg.SlotTaken, "Bác sĩ đã có ca trực này trong tuần.");
    }

    private async Task<DoctorShiftDto> GetShiftDtoAsync(int id)
    {
        var row = await _repository.GetDoctorShiftRowAsync(id)
            ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy ca trực.");
        return MapShift(row);
    }

    private DoctorShiftDto MapShift(DoctorShiftRow row) => new()
    {
        Id = row.Shift.Id,
        DoctorId = row.Shift.DoctorId,
        DoctorName = row.Doctor.FullName,
        LicenseNo = row.Doctor.LicenseNo,
        DayOfWeek = row.Shift.DayOfWeek,
        DayLabel = DayLabel(row.Shift.DayOfWeek),
        Shift = (byte)row.Shift.Shift,
        ShiftLabel = ShiftLabel((byte)row.Shift.Shift),
        IsActive = row.Shift.IsActive,
        RowVersion = row.Shift.ToRowVersion()
    };
    // Lưu trữ ca hiện tại và Ngày dùng để tìm ca trực 
    private sealed record ShiftContext (
        byte Shift,
        DateOnly ScheduleAt);
    // Để lưu cả 3 giá trị vào 1 đối tượng
    private sealed record ShiftTimes(
    TimeOnly MorningStart,
    TimeOnly AfternoonStart,
    TimeOnly NightStart);
    private byte? CurrentShift() => _clock.LocalNow.Hour < 12
        ? (byte)1
        : (byte)2;
    private async Task<ShiftTimes> GetShiftTimesAsync()
    {
        var morningStart = await _cfg.GetTimeAsync(
                ConfigKeys.ShiftMorningStart,
                new TimeOnly(7, 0));
        var afternoonStart = await _cfg.GetTimeAsync(
                ConfigKeys.ShiftAfternoonStart,
                new TimeOnly(14, 0));
        var nightStart = await _cfg.GetTimeAsync(
                ConfigKeys.ShiftNightStart,
                new TimeOnly(18, 0));
        return new ShiftTimes(
                MorningStart: morningStart,
                AfternoonStart: afternoonStart,
                NightStart: nightStart);
    }
    private ShiftContext ResolveCurrentShift(ShiftTimes shiftTimeConfig)
    {
        var localNow = _clock.LocalNow;
        var currentDate = DateOnly.FromDateTime(localNow);
        var currentTime = TimeOnly.FromDateTime(localNow);

        // Ca sáng theo giờ đã config
        if (currentTime >= shiftTimeConfig.MorningStart &&
            currentTime < shiftTimeConfig.AfternoonStart)
        {
            return new ShiftContext(
                Shift: 1,
                ScheduleAt: currentDate);
        }

        // Ca chiều theo giờ đã config
        if (currentTime >= shiftTimeConfig.AfternoonStart &&
            currentTime < shiftTimeConfig.NightStart)
        {
            return new ShiftContext(
                Shift: 2,
                ScheduleAt: currentDate);
        }

        // Ca đêm theo giờ đã config
        if (currentTime >= shiftTimeConfig.NightStart)
        {
            return new ShiftContext(
                Shift: 3,
                ScheduleAt: currentDate);
        }

        // Ca đêm: 00:00–06:59, lấy lịch hôm trước
        return new ShiftContext(
            Shift: 3,
            ScheduleAt: currentDate.AddDays(-1));
    }
    private (DateTime StartUtc, DateTime EndUtc) ResolveShiftUtcRange(DateOnly scheduleDate, byte shift, ShiftTimes shiftTimeConfig)
    {
        // Hai biến lưu thời điểm bắt đầu và kết thúc ca khám theo giờ điạ phương 
        DateTime startLocal;
        DateTime endLocal;
        switch (shift)
        {
            case 1:
                // Ca sáng
                startLocal = scheduleDate.ToDateTime(shiftTimeConfig.MorningStart); // Ghép DateOnly và thời gian thành 1 ngày DateTime
                endLocal = scheduleDate.ToDateTime(shiftTimeConfig.AfternoonStart);
                break;
            case 2:
                startLocal = scheduleDate.ToDateTime(shiftTimeConfig.AfternoonStart);
                endLocal = scheduleDate.ToDateTime(shiftTimeConfig.NightStart);
                break;
            case 3:
                // Ca đêm bắt đầu lúc 18:00 của ngày lịch trực.
                startLocal = scheduleDate.ToDateTime(shiftTimeConfig.NightStart);

                // Ca đêm kết thúc lúc 07:00 của ngày hôm sau,
                // nên phải cộng thêm một ngày trước khi ghép với MorningStart.
                endLocal = scheduleDate
                    .AddDays(1)
                    .ToDateTime(shiftTimeConfig.MorningStart);
                break;
            default:
                // Ngăn không cho các giá trị ngoài 1, 2, 3
                // được sử dụng để tính khoảng thời gian ca trực.
                throw AppException.BadRequest(
                    Msg.InvalidData,
                    "Ca trực không hợp lệ.");
        }
        var startUtc = _clock.ToUtc(startLocal);
        var endUtc = _clock.ToUtc(endLocal);
        return (
            StartUtc: startUtc,
            EndUtc: endUtc);
    }
    
    private static string ShiftLabel(byte s) => s switch { 1 => "Ca sáng", 2 => "Ca chiều", 3 => "Ca đêm", _ => "—" };
    private static string DayLabel(byte dow) => dow switch
    {
        0 => "Chủ nhật", 1 => "Thứ 2", 2 => "Thứ 3", 3 => "Thứ 4",
        4 => "Thứ 5", 5 => "Thứ 6", 6 => "Thứ 7", _ => "—"
    };
}
