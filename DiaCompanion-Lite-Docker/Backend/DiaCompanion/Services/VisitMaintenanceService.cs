using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Repositories;

namespace DiaCompanion.Api.Services;

/// <summary>
/// Bảo trì lượt khám theo NGÀY CLINIC, trong khi DB vẫn lưu UTC.
/// - Lượt đang mở không có dữ liệu nghiệp vụ: tự đóng cuối ngày.
/// - Lượt đã có dữ liệu: giữ InProgress và chuyển VisitDate sang ngày kế tiếp.
/// Không dùng SignalR; nếu cần thông báo thì chỉ ghi Notifications.
/// </summary>
public sealed class VisitMaintenanceService : IVisitMaintenanceService
{
    private readonly IRepository _repository;
    private readonly INotificationService _notify;
    private readonly IClinicClock _clock;
    private readonly IConfigService _config;
    private readonly ILogger<VisitMaintenanceService> _logger;

    public VisitMaintenanceService(
        IRepository repository,
        INotificationService notify,
        IClinicClock clock,
        IConfigService config,
        ILogger<VisitMaintenanceService> logger)
    {
        _repository = repository;
        _notify = notify;
        _clock = clock;
        _config = config;
        _logger = logger;
    }

    public async Task<VisitMaintenanceResult> ProcessAsync(bool includeToday, CancellationToken ct = default)
    {
        var today = _clock.LocalToday;
        var cutoffLocal = includeToday
            ? today.AddDays(1).ToDateTime(TimeOnly.MinValue)
            : today.ToDateTime(TimeOnly.MinValue);
        var cutoffUtc = _clock.ToUtc(cutoffLocal);

        var candidates = await _repository.GetOpenVisitMaintenanceCandidatesAsync(cutoffUtc, ct);
        if (candidates.Count == 0)
            return new VisitMaintenanceResult(0, 0, 0);

        var closeHour = Math.Clamp(await _config.GetIntAsync(ConfigKeys.CloseHour, 17), 0, 23);
        var autoClosed = 0;
        var carried = 0;

        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();

            var visit = await _repository.GetOpenVisitForDailyMaintenanceAsync(candidate.VisitId, ct);
            if (visit is null || visit.VisitDate >= cutoffUtc)
                continue;

            var originalLocal = _clock.ToLocal(visit.VisitDate) ?? visit.VisitDate;

            // Re-check ngay trước khi quyết định để tránh race condition.
            var hasData = candidate.HasClinicalData
                          || await _repository.VisitHasClinicalDataAsync(visit.Id, ct);

            if (!hasData)
            {
                var closeLocal = originalLocal.Date.AddHours(closeHour);
                var closedUtc = _clock.ToUtc(closeLocal);
                if (closedUtc > _clock.UtcNow)
                    closedUtc = _clock.UtcNow;

                visit.Status = VisitStatus.Completed;
                visit.ClosedAt = closedUtc;
                visit.Conclusion = "Hệ thống tự động đóng lượt khám cuối ngày vì không phát sinh dữ liệu lâm sàng.";
                visit.Referral = null;
                visit.RecheckMonths = null;
                autoClosed++;

                if (candidate.PatientUserId is int patientUserId)
                {
                    _notify.Push(
                        patientUserId,
                        NotificationType.Visit,
                        "Lượt khám đã được tự động đóng",
                        $"Lượt khám ngày {originalLocal:dd/MM/yyyy} đã được hệ thống đóng cuối ngày vì chưa phát sinh dữ liệu lâm sàng.",
                        nameof(Visit),
                        visit.Id);
                }
            }
            else
            {
                // Lượt tồn từ ngày trước -> đưa về hôm nay; lượt hôm nay sau giờ đóng -> ngày mai.
                var targetDate = DateOnly.FromDateTime(originalLocal) < today
                    ? today
                    : today.AddDays(1);
                var originalTime = TimeOnly.FromDateTime(originalLocal);
                var targetLocal = targetDate.ToDateTime(originalTime);
                visit.VisitDate = _clock.ToUtc(targetLocal);
                carried++;

                if (candidate.PatientUserId is int patientUserId)
                {
                    _notify.Push(
                        patientUserId,
                        NotificationType.Visit,
                        "Lượt khám được chuyển sang ngày tiếp theo",
                        $"Lượt khám đang xử lý đã có dữ liệu nên được giữ mở và chuyển sang ngày {targetDate:dd/MM/yyyy}.",
                        nameof(Visit),
                        visit.Id);
                }
            }
        }

        await _repository.CommitAsync(ct);

        _logger.LogInformation(
            "Daily visit maintenance: checked={Checked}, autoClosed={AutoClosed}, carried={Carried}",
            candidates.Count, autoClosed, carried);

        return new VisitMaintenanceResult(candidates.Count, autoClosed, carried);
    }
}
