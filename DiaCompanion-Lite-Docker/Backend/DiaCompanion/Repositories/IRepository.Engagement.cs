using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record NotificationPage(IReadOnlyList<NotificationDto> Items, int Total);
public sealed record SymptomPage(IReadOnlyList<(SymptomReport Report, string? ReplierName)> Items, int Total);
public sealed record FeedbackPage(IReadOnlyList<FeedbackDto> Items, int Total);

public partial interface IRepository
{
    Task<NotificationPage> GetNotificationPageAsync(int userId, DateTime now, PageQuery page, CancellationToken ct = default);
    Task<int> GetUnreadNotificationCountAsync(int userId, DateTime now, CancellationToken ct = default);
    Task<Notification?> GetNotificationForUpdateAsync(long id, int userId, CancellationToken ct = default);
    Task MarkAllNotificationsReadAsync(int userId, DateTime now, CancellationToken ct = default);

    Task<int?> GetLatestResponsibleDoctorIdAsync(int patientId, CancellationToken ct = default);
    Task<SymptomPage> GetSymptomPageAsync(int? patientId, int? responsibleDoctorId, bool pendingOnly, PageQuery page, CancellationToken ct = default);
    Task<SymptomReport?> GetSymptomReportAsync(int id, bool tracking, bool includePatient, CancellationToken ct = default);

    Task<bool> FeedbackExistsAsync(int patientId, int visitId, CancellationToken ct = default);
    Task<FeedbackPage> GetFeedbackPageAsync(byte? rating, string? keyword, string? normalizedKeyword,
        DateTime? fromUtc, DateTime? toExclusiveUtc, int? doctorId, PageQuery page, CancellationToken ct = default);
    Task<IReadOnlyList<byte>> GetFeedbackRatingsAsync(int? doctorId, CancellationToken ct = default);
}
