using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Repositories;

namespace DiaCompanion.Api.Services;

/// <summary>
/// Worker chạy cùng ASP.NET Core process.
/// - Mỗi 15 phút: xử lý lịch thuốc và lượt khám tồn.
/// - Recheck: chạy một lần/ngày từ clinic.open_hour, vẫn giữ các mốc 30/7/1/0 ngày
///   và bổ sung nhắc khi quá hạn.
/// - mọi thông báo được lưu bảng Notifications.
/// DB lưu UTC; mọi quyết định theo "hôm nay/ngày mai/giờ mở-đóng" dùng IClinicClock.
/// </summary>
public sealed class ClinicalReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClinicalReminderWorker> _logger;
    private DateOnly? _lastRecheckLocalDate;

    public ClinicalReminderWorker(IServiceScopeFactory scopeFactory, ILogger<ClinicalReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể chạy tác vụ clinical background.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();
        var notify = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClinicClock>();
        var config = scope.ServiceProvider.GetRequiredService<IConfigService>();
        var visitMaintenance = scope.ServiceProvider.GetRequiredService<IVisitMaintenanceService>();

        await ProcessMedicationRemindersAsync(repository, notify, clock, ct);

        // Trước giờ đóng cửa chỉ dọn các lượt tồn từ ngày trước.
        // Sau clinic.close_hour xử lý luôn các lượt đang mở của hôm nay.
        var closeHour = Math.Clamp(await config.GetIntAsync(ConfigKeys.CloseHour, 17), 0, 23);
        var includeToday = clock.LocalNow.Hour >= closeHour;
        await visitMaintenance.ProcessAsync(includeToday, ct);

        // Recheck chỉ cần chạy 1 lần/ngày. Nếu server restart trong ngày,
        // NotificationExistsAsync vẫn bảo đảm không gửi trùng.
        var today = clock.LocalToday;
        var openHour = Math.Clamp(await config.GetIntAsync(ConfigKeys.OpenHour, 8), 0, 23);
        if (clock.LocalNow.Hour >= openHour && _lastRecheckLocalDate != today)
        {
            await ProcessRecheckRemindersAsync(repository, notify, clock, ct);
            _lastRecheckLocalDate = today;
        }
    }

    private static async Task ProcessMedicationRemindersAsync(
        IRepository repository,
        INotificationService notify,
        IClinicClock clock,
        CancellationToken ct)
    {
        var nowUtc = clock.UtcNow;
        var missedBeforeUtc = nowUtc.AddHours(-2);

        await repository.MarkOverdueMedicationLogsMissedAsync(missedBeforeUtc, ct);

        var remindUntilUtc = nowUtc.AddMinutes(15);
        var logs = await repository.GetMedicationReminderCandidatesAsync(
            missedBeforeUtc, remindUntilUtc, 500, ct);

        foreach (var log in logs)
        {
            var item = log.PrescriptionItem!;
            var prescription = item.Prescription!;
            if (prescription.Patient?.UserId is not int userId)
                continue;

            var localTime = clock.ToLocal(log.ScheduledAt) ?? log.ScheduledAt;
            notify.Push(
                userId,
                NotificationType.Medication,
                "Đến giờ dùng thuốc",
                $"{item.DrugName} – {item.Dose}, lúc {localTime:HH:mm}. Vui lòng xác nhận sau khi dùng.",
                nameof(MedicationLog),
                log.Id);

            log.ReminderSentAt = nowUtc;
        }

        if (logs.Count > 0)
            await repository.CommitAsync(ct);
    }

    //private static async Task ProcessRecheckRemindersAsync(
    //    IRepository repository,
    //    INotificationService notify,
    //    IClinicClock clock,
    //    CancellationToken ct)
    //{
    //    var today = clock.LocalToday;
    //    var latestVisits = await repository.GetRecheckReminderCandidatesAsync(ct);
    //    if (latestVisits.Count == 0)
    //        return;

    //    var added = 0;

    //    foreach (var visit in latestVisits)
    //    {
    //        ct.ThrowIfCancellationRequested();

    //        // Có lượt khám có ý nghĩa mới hơn lần dùng làm mốc recheck => đã quay lại.
    //        if (visit.LatestVisitDate is DateTime latestVisitDate && latestVisitDate > visit.ClosedAt)
    //            continue;

    //        var closedLocal = clock.ToLocal(visit.ClosedAt) ?? visit.ClosedAt;
    //        var dueDate = DateOnly.FromDateTime(closedLocal.AddMonths(visit.RecheckMonths));
    //        var daysUntilDue = dueDate.DayNumber - today.DayNumber;
    //        var daysPastDue = -daysUntilDue;

    //        // GIỮ NGUYÊN các mốc cũ 30/7/1 ngày và đúng ngày.
    //        // Bổ sung: ngày đầu tiên quá hạn và sau đó mỗi 7 ngày nếu vẫn chưa quay lại.
    //        var shouldSend = daysUntilDue is 30 or 7 or 1 or 0
    //                         || daysPastDue == 1;
    //        if (!shouldSend)
    //            continue;


    //        // Giữ nguyên format title cũ để NotificationExistsAsync nhận ra
    //        // các thông báo 30/7/1/0 ngày đã phát hành trước khi nâng cấp code.
    //        var title = daysUntilDue >= 0
    //            ? $"Nhắc tái tầm soát {dueDate:dd/MM/yyyy}"
    //            : $"Tái tầm soát quá hạn {daysPastDue} ngày";

    //        var alreadySent = await repository.NotificationExistsAsync(
    //            visit.UserId,
    //            NotificationType.Recheck,
    //            title,
    //            nameof(Visit),
    //            visit.VisitId,
    //            ct);
    //        if (alreadySent)
    //            continue;

    //        var message = daysUntilDue switch
    //        {
    //            > 1 => $"Ngày tái tầm soát dự kiến của bạn là {dueDate:dd/MM/yyyy} (còn {daysUntilDue} ngày).",
    //            1 => $"Ngày mai ({dueDate:dd/MM/yyyy}) là ngày tái tầm soát dự kiến của bạn.",
    //            0 => $"Hôm nay ({dueDate:dd/MM/yyyy}) là ngày tái tầm soát dự kiến của bạn.",
    //            _ => $"Bạn đã quá ngày tái tầm soát dự kiến {daysPastDue} ngày. Vui lòng liên hệ cơ sở y tế để tái khám."
    //        };

    //        notify.Push(
    //            visit.UserId,
    //            NotificationType.Recheck,
    //            title,
    //            message,
    //            nameof(Visit),
    //            visit.VisitId);
    //        added++;
    //    }

    //    if (added > 0)
    //        await repository.CommitAsync(ct);
    //}

    private static async Task ProcessRecheckRemindersAsync(
        IRepository repository,
        INotificationService notify,
        IClinicClock clock,
        CancellationToken ct)
    {
        var today = clock.LocalToday;
        var latestVisits = await repository.GetRecheckReminderCandidatesAsync(ct);
        if (latestVisits.Count == 0)
            return;

        foreach (var visit in latestVisits)
        {
            // Có lượt khám mới sau lần đóng này nghĩa là bệnh nhân đã quay lại.
            if (visit.LatestVisitDate is DateTime latestVisitDate && latestVisitDate > visit.ClosedAt)
                continue;

            var closedLocal = clock.ToLocal(visit.ClosedAt) ?? visit.ClosedAt;

            var dueDate = DateOnly.FromDateTime(
                closedLocal.AddMonths(visit.RecheckMonths)
            );

            var daysUntilDue = dueDate.DayNumber - today.DayNumber;
            var daysPastDue = -daysUntilDue;


            // ==============================
            // ĐÃ QUÁ HẠN
            // ==============================
            if (daysPastDue > 0)
            {
                const string title = "Tái tầm soát đã quá hạn";

                var alreadySent = await repository.NotificationExistsAsync(
                    visit.UserId,
                    NotificationType.Recheck,
                    title,
                    nameof(Visit),
                    visit.VisitId,
                    ct
                );

                if (!alreadySent)
                {
                    notify.Push(
                        visit.UserId,
                        NotificationType.Recheck,
                        title,
                        $"Bạn đã quá ngày tái tầm soát dự kiến " +
                        $"{daysPastDue} ngày (hạn {dueDate:dd/MM/yyyy}). " +
                        $"Vui lòng liên hệ cơ sở y tế.",
                        nameof(Visit),
                        visit.VisitId
                    );
                }

                continue;
            }


            // ==============================
            // CHƯA QUÁ HẠN
            // ==============================
            var shouldSend =
                daysUntilDue is 30 or 7 or 1 or 0;

            if (!shouldSend)
                continue;

            var reminderTitle =
                $"Nhắc tái tầm soát {dueDate:dd/MM/yyyy}";

            var reminderAlreadySent =
                await repository.NotificationExistsAsync(
                    visit.UserId,
                    NotificationType.Recheck,
                    reminderTitle,
                    nameof(Visit),
                    visit.VisitId,
                    ct
                );

            if (reminderAlreadySent)
                continue;

            var message = daysUntilDue switch
            {
                > 1 =>
                    $"Ngày tái tầm soát dự kiến của bạn là " +
                    $"{dueDate:dd/MM/yyyy} (còn {daysUntilDue} ngày).",

                1 =>
                    $"Ngày mai ({dueDate:dd/MM/yyyy}) " +
                    $"là ngày tái tầm soát dự kiến của bạn.",

                0 =>
                    $"Hôm nay ({dueDate:dd/MM/yyyy}) " +
                    $"là ngày tái tầm soát dự kiến của bạn.",

                _ => ""
            };

            notify.Push(
                visit.UserId,
                NotificationType.Recheck,
                reminderTitle,
                message,
                nameof(Visit),
                visit.VisitId
            );
        }

        await repository.CommitAsync(ct);
    }
}
