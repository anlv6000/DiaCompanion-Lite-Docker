using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Services;

public interface IEngagementService
{
    Task<ActionResult<PagedResult<NotificationDto>>> Notifications(PageQuery page);
    Task<IActionResult> UnreadCount();
    Task<IActionResult> MarkRead(long id);
    Task<IActionResult> MarkAllRead();
    Task<ActionResult<SymptomReportDto>> ReportSymptom(CreateSymptomRequest req);
    Task<ActionResult<PagedResult<SymptomReportDto>>> Symptoms(
        int? patientId, bool pendingOnly = false, PageQuery? page = null);
    Task<IActionResult> Reply(int id, DoctorReplyRequest req);
    Task<IActionResult> CreateFeedback(CreateFeedbackRequest req);
    Task<ActionResult<PagedResult<FeedbackDto>>> Feedbacks(
        byte? rating, string? q, DateOnly? from, DateOnly? to, PageQuery page);
    Task<IActionResult> FeedbackSummary();
    Task<ActionResult<PagedResult<SymptomReportDto>>> MySymptoms(
    bool pendingOnly = false,
    PageQuery? page = null);
}
