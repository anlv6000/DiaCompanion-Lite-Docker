using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Repositories;

namespace DiaCompanion.Api.Services;

/// <summary>
/// Ghi thông báo vào CSDL. Không dùng SignalR/FCM/APNs trong phiên bản này;
/// web/mobile đọc thông báo qua API engagement khi mở hoặc refresh.
/// Timestamp luôn lưu UTC; API Notifications() đổi CreatedAt sang giờ clinic.
/// </summary>
public interface INotificationService
{
    Notification Push(int userId, NotificationType type, string title, string message,
        string? linkEntity = null, int? linkEntityId = null);

    Notification? PushToPatient(Patient patient, NotificationType type, string title, string message,
        string? linkEntity = null, int? linkEntityId = null);
}

public sealed class NotificationService : INotificationService
{
    private readonly IRepository _repository;
    private readonly IClinicClock _clock;

    public NotificationService(IRepository repository, IClinicClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public Notification Push(int userId, NotificationType type, string title, string message,
        string? linkEntity = null, int? linkEntityId = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            LinkEntity = linkEntity,
            LinkEntityId = linkEntityId,
            CreatedAt = _clock.UtcNow,
            ExpiresAt = _clock.UtcNow.AddDays(90)
        };

        _repository.Add(notification);
        return notification;
    }

    public Notification? PushToPatient(Patient patient, NotificationType type, string title, string message,
        string? linkEntity = null, int? linkEntityId = null)
    {
        if (patient.UserId is not int userId)
            return null;

        return Push(userId, type, title, message, linkEntity, linkEntityId);
    }
}
