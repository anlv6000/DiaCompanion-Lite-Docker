using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public async Task<NotificationPage> GetNotificationPageAsync(int userId, DateTime now, PageQuery page, CancellationToken ct = default)
    {
        var query = _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && (n.ExpiresAt == null || n.ExpiresAt > now));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(n => n.CreatedAt)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = (byte)n.Type,
                Title = n.Title,
                Message = n.Message,
                LinkEntity = n.LinkEntity,
                LinkEntityId = n.LinkEntityId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            }).ToListAsync(ct);
        return new NotificationPage(items, total);
    }

    public Task<int> GetUnreadNotificationCountAsync(int userId, DateTime now, CancellationToken ct = default) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead && (n.ExpiresAt == null || n.ExpiresAt > now), ct);

    public Task<Notification?> GetNotificationForUpdateAsync(long id, int userId, CancellationToken ct = default) =>
        _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);

    public Task MarkAllNotificationsReadAsync(int userId, DateTime now, CancellationToken ct = default) =>
        _db.Notifications.Where(n => n.UserId == userId && !n.IsRead && (n.ExpiresAt == null || n.ExpiresAt > now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, now), ct);

    public Task<int?> GetLatestResponsibleDoctorIdAsync(int patientId, CancellationToken ct = default) =>
        _db.Visits.AsNoTracking().Where(v => v.MedicalRecord.PatientId == patientId && v.DoctorId != null)
            .OrderByDescending(v => v.VisitDate).ThenByDescending(v => v.Id)
            .Select(v => v.DoctorId).FirstOrDefaultAsync(ct);

    public async Task<SymptomPage> GetSymptomPageAsync(
        int? patientId, int? responsibleDoctorId, bool pendingOnly, PageQuery page, CancellationToken ct = default)
    {
        var query = _db.SymptomReports.AsNoTracking()
            .Include(s => s.Patient).Include(s => s.ResponsibleDoctor).AsQueryable();
        if (patientId is int pid) query = query.Where(s => s.PatientId == pid);
        if (responsibleDoctorId is int did)
        {
            query = query.Where(s =>
                s.ResponsibleDoctorId == null ||
                s.ResponsibleDoctorId == did);
        }
        if (pendingOnly) query = query.Where(s => s.DoctorReply == null);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(s => s.Severity).ThenByDescending(s => s.CreatedAt)
            .Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        var ids = rows.Where(r => r.RepliedBy.HasValue).Select(r => r.RepliedBy!.Value).Distinct().ToArray();
        var names = ids.Length == 0
            ? new Dictionary<int, string>()
            : await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var items = rows.Select(r => (r, r.RepliedBy is int rid && names.TryGetValue(rid, out var name) ? name : null)).ToList();
        return new SymptomPage(items, total);
    }

    public Task<SymptomReport?> GetSymptomReportAsync(
        int id, bool tracking, bool includePatient, CancellationToken ct = default)
    {
        IQueryable<SymptomReport> query = _db.SymptomReports.Where(s => s.Id == id);
        if (includePatient) query = query.Include(s => s.Patient).Include(s => s.ResponsibleDoctor);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(ct);
    }

    public Task<bool> FeedbackExistsAsync(int patientId, int visitId, CancellationToken ct = default) =>
        _db.Feedbacks.AnyAsync(f => f.PatientId == patientId && f.VisitId == visitId, ct);

    public async Task<FeedbackPage> GetFeedbackPageAsync(
        byte? rating,
        string? keyword,
        string? normalizedKeyword,
        DateTime? fromUtc,
        DateTime? toExclusiveUtc,
        int? doctorId,
        PageQuery page,
        CancellationToken ct = default)
    {
        var query = _db.Feedbacks.AsNoTracking().Include(f => f.Patient).AsQueryable();

        // Doctor chỉ thấy feedback của lượt khám mà mình là bác sĩ phụ trách.
        // Feedback không gắn Visit không thuộc phạm vi của Doctor; Admin vẫn thấy tất cả.
        if (doctorId is int did)
            query = query.Where(f =>
                f.VisitId.HasValue &&
                _db.Visits.Any(v => v.Id == f.VisitId.Value && v.DoctorId == did));

        if (rating is byte value) query = query.Where(f => f.Rating == value);
        if (fromUtc is DateTime from) query = query.Where(f => f.CreatedAt >= from);
        if (toExclusiveUtc is DateTime to) query = query.Where(f => f.CreatedAt < to);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            var normalized = normalizedKeyword?.Trim() ?? term;
            query = query.Where(f =>
                EF.Functions.Like(f.Patient!.Code, $"%{term}%") ||
                EF.Functions.Like(f.Patient.FullNameSearch!, $"%{normalized}%") ||
                (f.Comment != null && EF.Functions.Like(f.Comment, $"%{term}%")));
        }

        var total = await query.CountAsync(ct);
        query = page.Sort?.Trim().ToLowerInvariant() switch
        {
            "rating" => page.Desc
                ? query.OrderByDescending(f => f.Rating).ThenByDescending(f => f.CreatedAt)
                : query.OrderBy(f => f.Rating).ThenByDescending(f => f.CreatedAt),
            "patient" => page.Desc
                ? query.OrderByDescending(f => f.Patient!.FullName).ThenByDescending(f => f.CreatedAt)
                : query.OrderBy(f => f.Patient!.FullName).ThenByDescending(f => f.CreatedAt),
            _ => page.Desc ? query.OrderBy(f => f.CreatedAt) : query.OrderByDescending(f => f.CreatedAt)
        };

        var items = await query.Skip(page.Skip).Take(page.PageSize)
            .Select(f => new FeedbackDto
            {
                Id = f.Id,
                PatientId = f.PatientId,
                PatientCode = f.Patient!.Code,
                PatientName = f.Patient.FullName,
                VisitId = f.VisitId,
                Rating = f.Rating,
                Tags = f.Tags,
                Comment = f.Comment,
                CreatedAt = f.CreatedAt
            }).ToListAsync(ct);
        return new FeedbackPage(items, total);
    }

    public async Task<IReadOnlyList<byte>> GetFeedbackRatingsAsync(
        int? doctorId,
        CancellationToken ct = default)
    {
        var query = _db.Feedbacks.AsNoTracking().AsQueryable();

        if (doctorId is int did)
            query = query.Where(f =>
                f.VisitId.HasValue &&
                _db.Visits.Any(v => v.Id == f.VisitId.Value && v.DoctorId == did));

        return await query.Select(f => f.Rating).ToListAsync(ct);
    }
}
