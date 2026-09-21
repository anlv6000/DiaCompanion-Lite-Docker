namespace DiaCompanion.Api.Services;

public sealed record VisitMaintenanceResult(
    int Checked,
    int AutoClosed,
    int CarriedForward);

public interface IVisitMaintenanceService
{
    /// <summary>
    /// Xử lý các lượt khám đang mở trước cutoff. Nếu includeToday=true thì
    /// cutoff là đầu ngày mai theo giờ clinic; ngược lại chỉ xử lý ngày cũ.
    /// </summary>
    Task<VisitMaintenanceResult> ProcessAsync(bool includeToday, CancellationToken ct = default);
}
