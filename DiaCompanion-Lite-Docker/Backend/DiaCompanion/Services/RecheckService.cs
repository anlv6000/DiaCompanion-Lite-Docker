using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Api.Dtos;

namespace DiaCompanion.Api.Services;

/// <summary>UC-48 — nghiệp vụ tái tầm soát; dữ liệu được lấy qua Repository.</summary>
public class RecheckService : BaseService, IRecheckService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IClinicClock _clock;

    public RecheckService(IRepository repository, ICurrentUser me, IClinicClock clock)
    {
        _repository = repository;
        _me = me;
        _clock = clock;
    }

    public async Task<ActionResult<RecheckDto>> Mine()
    {
        var item = await BuildAsync(RequireMyPatientId(_me));
        if (item is null)
            return Ok(new
            {
                hasRecheck = false,
                message = "Bạn chưa có lịch tái tầm soát. Lịch sẽ được xác định sau lần khám tiếp theo."
            });
        return Ok(item);
    }

    public async Task<ActionResult<RecheckDto>> ForPatient(int patientId)
    {
        var item = await BuildAsync(patientId);
        if (item is null)
            throw AppException.NotFound(Msg.LoadFailed,
                "Bệnh nhân chưa có lượt khám hoàn tất nào để tính ngày tái tầm soát.");
        return Ok(item);
    }

    public async Task<ActionResult<PagedResult<RecheckDto>>> Due(
    [FromQuery] bool overdueOnly = false,
    [FromQuery] int withinDays = 30,
    [FromQuery] PageQuery? page = null)
    {
        page ??= new PageQuery();

        var today = _clock.LocalToday;

        var candidates =
            await _repository.GetRecheckCandidatesAsync();

        var computed = candidates
            .Select(c =>
            {
                // Chuyển ClosedAt sang giờ địa phương
                var closedAtLocal =
                    _clock.ToLocal(c.ClosedAt)!.Value;

                // Ngày phải tái tầm soát
                var due = DateOnly.FromDateTime(
                    closedAtLocal.AddMonths(c.RecheckMonths)
                );

                // > 0 : quá hạn
                // = 0 : đến hạn hôm nay
                // < 0 : chưa đến hạn
                var pastDue =
                    today.DayNumber - due.DayNumber;

                return new
                {
                    Candidate = c,
                    ClosedAtLocal = closedAtLocal,
                    Due = due,
                    PastDue = pastDue
                };
            })

            // overdueOnly = true:
            // chỉ lấy bệnh nhân đã quá hạn
            //
            // overdueOnly = false:
            // lấy bệnh nhân quá hạn hoặc sắp đến hạn
            // trong withinDays ngày
            .Where(x =>
                overdueOnly
                    ? x.PastDue > 0
                    : x.PastDue >= -withinDays
            )

            // Quá hạn lâu nhất lên đầu
            .OrderByDescending(x => x.PastDue)

            .ToList();

        var total = computed.Count;

        var items = computed
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(x =>
                Map(
                    x.Candidate,
                    x.ClosedAtLocal,
                    x.Due,
                    x.PastDue
                ))
            .ToList();

        return Ok(new PagedResult<RecheckDto>
        {
            Items = items,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalItems = total
        });
    }

    public async Task<IActionResult> OverdueCount()
    {
        var today = _clock.LocalToday;

        var candidates =
            await _repository.GetRecheckCandidatesAsync();

        var overdue = candidates.Count(c =>
        {
            var closedAtLocal =
                _clock.ToLocal(c.ClosedAt)!.Value;

            var due = DateOnly.FromDateTime(
                closedAtLocal.AddMonths(c.RecheckMonths)
            );

            return due < today;
        });

        return Ok(new { overdue });
    }

    private async Task<RecheckDto?> BuildAsync(int patientId)
    {
        EnsureCanReadPatient(_me, patientId);
        var candidate = await _repository.GetRecheckCandidateAsync(patientId);
        if (candidate is null) return null;
        var closedAtLocal = _clock.ToLocal(candidate.ClosedAt)!.Value;
        var due = DateOnly.FromDateTime(closedAtLocal.AddMonths(candidate.RecheckMonths));
        var pastDue = _clock.LocalToday.DayNumber - due.DayNumber;
        return Map(candidate, closedAtLocal, due, pastDue);
    }

    private static RecheckDto Map(RecheckCandidate candidate, DateTime closedAtLocal, DateOnly due, int pastDue) => new()
    {
        PatientId = candidate.PatientId,
        PatientCode = candidate.PatientCode,
        PatientName = candidate.PatientName,
        PatientPhone = candidate.PatientPhone,
        LastVisitId = candidate.LastVisitId,
        LastVisitClosedAt = closedAtLocal,
        LastConfirmedGrade = candidate.LastConfirmedGrade,
        LastConfirmedGradeLabel = candidate.LastConfirmedGrade is byte grade ? DiagnosesService.GradeLabel(grade) : null,
        Referral = (byte?)candidate.Referral,
        RecheckMonths = candidate.RecheckMonths,
        DueDate = due,
        DaysPastDue = pastDue,
        IsOverdue = pastDue > 0,
        StatusLabel = Label(pastDue)
    };

    private static string Label(int daysPastDue) => daysPastDue switch
    {
        > 90 => "Quá hạn trên 3 tháng",
        > 0 => $"Quá hạn {daysPastDue} ngày",
        > -8 => "Đến hạn trong tuần này",
        > -31 => "Đến hạn trong tháng này",
        _ => $"Còn {-daysPastDue} ngày"
    };
}
