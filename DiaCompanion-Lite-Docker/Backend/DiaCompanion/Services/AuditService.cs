using System.Text.Json;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

/// <summary>
/// NF-13 / QA-10. CẢNH BÁO: OldValue/NewValue chứa dữ liệu bệnh án, nên bảng
/// AuditLogs TỰ NÓ là dữ liệu y tế — chỉ Admin đọc được (SCR-24) và phải giữ
/// đúng thời hạn lưu trữ như hồ sơ gốc. Không phải "log kỹ thuật".
/// </summary>
public interface IAuditService
{
    Task LogAsync(string action, string entityType, int? entityId = null,
                  object? oldValue = null, object? newValue = null, string? detail = null);
}

public class AuditService : IAuditService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AuditService(IRepository repository, ICurrentUser me) { _repository = repository; _me = me; }

    public Task LogAsync(string action, string entityType, int? entityId = null,
                         object? oldValue = null, object? newValue = null, string? detail = null)
    {
        _repository.Add(new AuditLog
        {
            UserId = _me.Id,
            UserName = _me.FullName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = oldValue is null ? null : JsonSerializer.Serialize(oldValue, JsonOpts),
            NewValue = newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOpts),
            Detail = detail,
            IpAddress = _me.Ip,
            CreatedAt = DateTime.UtcNow
        });
        // Cố ý KHÔNG SaveChanges ở đây: bản ghi audit phải nằm cùng transaction
        // với thao tác nghiệp vụ. Nếu thao tác lỗi thì audit cũng không được ghi.
        return Task.CompletedTask;
    }
}
