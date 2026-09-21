using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Services;

/// <summary>UC-49..52 — nghiệp vụ thông báo, triệu chứng và phản hồi dịch vụ.</summary>
public class EngagementService : BaseService, IEngagementService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly ISymptomAdviceService _advice;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;
    private readonly IClinicClock _clock;

    public EngagementService(
        IRepository repository,
        ICurrentUser me,
        ISymptomAdviceService advice,
        INotificationService notify,
        IAuditService audit,
        IClinicClock clock)
    {
        _repository = repository;
        _me = me;
        _advice = advice;
        _notify = notify;
        _audit = audit;
        _clock = clock;
    }

    public async Task<ActionResult<PagedResult<NotificationDto>>> Notifications(PageQuery page)
    {
        var result = await _repository.GetNotificationPageAsync(_me.RequireId(), _clock.UtcNow, page);
        return Ok(new PagedResult<NotificationDto>
        {
            Items = result.Items.Select(n =>
            {
                n.CreatedAt = _clock.ToLocal(n.CreatedAt)!.Value;
                return n;
            }).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalItems = result.Total
        });
    }

    public async Task<IActionResult> UnreadCount()
    {
        var count = await _repository.GetUnreadNotificationCountAsync(_me.RequireId(), _clock.UtcNow);
        return Ok(new { count });
    }

    public async Task<IActionResult> MarkRead(long id)
    {
        var notification = await _repository.GetNotificationForUpdateAsync(id, _me.RequireId())
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy thông báo.");
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = _clock.UtcNow;
            await _repository.CommitAsync();
        }
        return Ok(new { message = "Đã đánh dấu đã đọc." });
    }

    public async Task<IActionResult> MarkAllRead()
    {
        await _repository.MarkAllNotificationsReadAsync(_me.RequireId(), _clock.UtcNow);
        return Ok(new { message = "Đã đánh dấu tất cả đã đọc." });
    }

    public async Task<ActionResult<SymptomReportDto>> ReportSymptom(CreateSymptomRequest req)
    {
        var patientId = RequireMyPatientId(_me);
        var responsibleDoctorId = await _repository.GetLatestResponsibleDoctorIdAsync(patientId);
        var report = new SymptomReport
        {
            PatientId = patientId,
            ResponsibleDoctorId = responsibleDoctorId,
            Symptoms = req.Symptoms.Trim(),
            Severity = req.Severity,
            Description = req.Description?.Trim(),
            OnsetNote = req.OnsetNote?.Trim(),
            AutoAdvice = _advice.Generate(req.Severity),
            CreatedAt = _clock.UtcNow
        };

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            _repository.Add(report);
            await _repository.CommitAsync();

            if (responsibleDoctorId is int doctorId)
            {
                var patient = await _repository.GetPatientAsync(patientId)
                    ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");
                _notify.Push(
                    doctorId,
                    NotificationType.Result,
                    req.Severity == SymptomSeverity.Severe ? "Báo triệu chứng NẶNG" : "Bệnh nhân báo triệu chứng",
                    $"{patient.FullName} ({patient.Code}): {report.Symptoms}",
                    nameof(SymptomReport),
                    report.Id);
            }

            await _audit.LogAsync(
                AuditAction.SymptomReport,
                nameof(SymptomReport),
                report.Id,
                null,
                new { report.PatientId, report.ResponsibleDoctorId, severity = report.Severity.ToString() });
            await _repository.CommitAsync();
        });

        return Ok(await GetSymptomDtoAsync(report.Id));
    }

    public async Task<ActionResult<PagedResult<SymptomReportDto>>> Symptoms(
        int? patientId,
        bool pendingOnly = false,
        PageQuery? page = null)
    {
        page ??= new PageQuery();

        int? patientScope = null;
        int? doctorScope = null;

        if (IsPatientOnly(_me))
        {
            // Patient chỉ được xem report của chính mình.
            patientScope = RequireMyPatientId(_me);
        }
        else if (_me.IsInRole(Roles.Doctor))
        {
            // Doctor:
            // - xem report được giao cho mình
            // - xem report chưa có bác sĩ phụ trách
            doctorScope = _me.RequireId();

            // Nếu truyền patientId thì filter thêm theo bệnh nhân.
            patientScope = patientId;
        }
        else
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Chỉ bệnh nhân và bác sĩ được xem báo cáo triệu chứng.");
        }

        var result = await _repository.GetSymptomPageAsync(
            patientScope,
            doctorScope,
            pendingOnly,
            page);

        var items = result.Items
            .Select(x => MapSymptom(x.Report, x.ReplierName))
            .ToList();

        return Ok(new PagedResult<SymptomReportDto>
        {
            Items = items,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalItems = result.Total
        });
    }

    public async Task<IActionResult> Reply(int id, DoctorReplyRequest req)
    {
        var doctorId = _me.RequireId();

        var report = await _repository.GetSymptomReportAsync(
            id,
            tracking: true,
            includePatient: true)
            ?? throw AppException.NotFound(
                Msg.LoadFailed,
                "Không tìm thấy báo cáo triệu chứng.");

        // Nếu report đã có bác sĩ phụ trách
        // thì chỉ bác sĩ đó được phản hồi.
        //
        // Nếu ResponsibleDoctorId == null:
        // bệnh nhân chưa có bác sĩ phụ trách tại thời điểm báo cáo,
        // bất kỳ bác sĩ nào cũng có thể phản hồi.
        if (report.ResponsibleDoctorId.HasValue &&
            report.ResponsibleDoctorId.Value != doctorId)
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Báo cáo triệu chứng này thuộc bác sĩ phụ trách khác.");
        }

        // Dùng RowVersion client gửi lên để chống concurrent update.
        _repository.ApplyOriginalRowVersion(
            report,
            req.RowVersion);

        var oldReply = report.DoctorReply;
        var oldResponsibleDoctorId = report.ResponsibleDoctorId;

        // Nếu chưa có bác sĩ phụ trách,
        // bác sĩ phản hồi thành công đầu tiên sẽ trở thành bác sĩ của report.
        if (!report.ResponsibleDoctorId.HasValue)
        {
            report.ResponsibleDoctorId = doctorId;
        }

        report.DoctorReply = req.Reply.Trim();
        report.RepliedBy = doctorId;
        report.RepliedAt = _clock.UtcNow;

        await _audit.LogAsync(
            AuditAction.SymptomReply,
            nameof(SymptomReport),
            report.Id,
            new
            {
                doctorReply = oldReply,
                responsibleDoctorId = oldResponsibleDoctorId
            },
            new
            {
                report.DoctorReply,
                report.RepliedBy,
                report.ResponsibleDoctorId
            });

        // Nếu hai bác sĩ load cùng RowVersion:
        //
        // Doctor A commit trước -> thành công -> RowVersion đổi
        // Doctor B commit sau  -> RowVersion cũ -> concurrency conflict
        if (!await _repository.TryCommitAsync())
        {
            throw AppException.Conflict(
                Msg.StaleVersion,
                "Báo cáo triệu chứng đã được bác sĩ khác xử lý. Vui lòng tải lại.");
        }

        if (report.Patient is not null)
        {
            _notify.PushToPatient(
                report.Patient,
                NotificationType.Result,
                "Bác sĩ đã trả lời",
                "Bác sĩ đã phản hồi báo cáo triệu chứng của bạn.",
                nameof(SymptomReport),
                report.Id);
        }

        return Ok(new
        {
            message = "Đã gửi phản hồi tới bệnh nhân.",
            rowVersion = report.ToRowVersion()
        });
    }
    public async Task<IActionResult> CreateFeedback(CreateFeedbackRequest req)
    {
        var patientId = RequireMyPatientId(_me);
        if (req.VisitId is int visitId)
        {
            var visit = await _repository.GetVisitForUpdateAsync(visitId)
                ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy lượt khám.");
            if (visit.MedicalRecord.PatientId != patientId)
                throw AppException.Forbidden(Msg.Forbidden, "Bạn không có quyền phản hồi lượt khám này.");
            if (visit.Status != VisitStatus.Completed)
                throw AppException.BadRequest(Msg.ApptImmutable, "Chỉ có thể phản hồi sau khi lượt khám đã hoàn tất.");
            if (await _repository.FeedbackExistsAsync(patientId, visitId))
                throw AppException.Conflict(Msg.ConcurrentEdit, "Bạn đã gửi phản hồi cho lượt khám này.");
        }

        var feedback = new Feedback
        {
            PatientId = patientId,
            VisitId = req.VisitId,
            Rating = req.Rating,
            Tags = req.Tags?.Trim(),
            Comment = req.Comment?.Trim(),
            CreatedAt = _clock.UtcNow
        };

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            _repository.Add(feedback);
            await _repository.CommitAsync();
            await _audit.LogAsync(
                AuditAction.FeedbackCreate,
                nameof(Feedback),
                feedback.Id,
                null,
                new { feedback.PatientId, feedback.VisitId, feedback.Rating });
            await _repository.CommitAsync();
        });

        return Ok(new { message = "Cảm ơn bạn đã gửi phản hồi.", feedbackId = feedback.Id });
    }

    public async Task<ActionResult<PagedResult<FeedbackDto>>> Feedbacks(
        byte? rating,
        string? q,
        DateOnly? from,
        DateOnly? to,
        PageQuery page)
    {
        DateTime? fromUtc = from is DateOnly fromDate
            ? _clock.ToUtc(fromDate.ToDateTime(TimeOnly.MinValue))
            : null;
        DateTime? toExclusiveUtc = to is DateOnly toDate
            ? _clock.ToUtc(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue))
            : null;
        var normalized = string.IsNullOrWhiteSpace(q) ? null : VietnameseText.RemoveDiacritics(q.Trim());
        var doctorId = _me.IsInRole(Roles.Doctor) && !_me.IsInRole(Roles.Admin)
            ? _me.RequireId()
            : (int?)null;
        var result = await _repository.GetFeedbackPageAsync(
            rating, q, normalized, fromUtc, toExclusiveUtc, doctorId, page);
        return Ok(new PagedResult<FeedbackDto>
        {
            Items = result.Items.Select(f =>
            {
                f.CreatedAt = _clock.ToLocal(f.CreatedAt)!.Value;
                return f;
            }).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalItems = result.Total
        });
    }

    public async Task<IActionResult> FeedbackSummary()
    {
        var doctorId = _me.IsInRole(Roles.Doctor) && !_me.IsInRole(Roles.Admin)
            ? _me.RequireId()
            : (int?)null;
        var ratings = await _repository.GetFeedbackRatingsAsync(doctorId);
        return Ok(new
        {
            total = ratings.Count,
            average = ratings.Count > 0 ? Math.Round(ratings.Average(r => (double)r), 2) : 0,
            distribution = Enumerable.Range(1, 5).ToDictionary(i => i.ToString(), i => ratings.Count(r => r == i))
        });
    }

    private async Task<SymptomReportDto> GetSymptomDtoAsync(int id)
    {
        var report = await _repository.GetSymptomReportAsync(id, tracking: false, includePatient: true)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy báo cáo triệu chứng.");
        return MapSymptom(report, null);
    }

    private SymptomReportDto MapSymptom(SymptomReport report, string? replierName) => new()
    {
        Id = report.Id,
        Symptoms = report.Symptoms,
        Severity = (byte)report.Severity,
        Description = report.Description,
        OnsetNote = report.OnsetNote,
        AutoAdvice = report.AutoAdvice,
        DoctorReply = report.DoctorReply,
        RepliedByName = replierName,
        RepliedAt = _clock.ToLocal(report.RepliedAt),
        CreatedAt = _clock.ToLocal(report.CreatedAt)!.Value,
        PatientName = report.Patient?.FullName ?? "",
        ResponsibleDoctorId = report.ResponsibleDoctorId,
        ResponsibleDoctorName = report.ResponsibleDoctor?.FullName,
        RowVersion = report.ToRowVersion(),
        State = report.DoctorReply is not null
            ? "Bác sĩ đã trả lời"
            : report.ResponsibleDoctorId is null ? "Chưa xác định bác sĩ phụ trách" : "Chờ bác sĩ xem"
    };

    public async Task<ActionResult<PagedResult<SymptomReportDto>>> MySymptoms(
    bool pendingOnly = false,
    PageQuery? page = null)
    {
        page ??= new PageQuery();

        var patientId = RequireMyPatientId(_me);

        var result = await _repository.GetSymptomPageAsync(
            patientId,
            null,
            pendingOnly,
            page);

        var items = result.Items
            .Select(x => MapSymptom(x.Report, x.ReplierName))
            .ToList();

        return Ok(new PagedResult<SymptomReportDto>
        {
            Items = items,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalItems = result.Total
        });
    }
}
