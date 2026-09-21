namespace DiaCompanion.Api.Services;

/// <summary>
/// QT-10: hệ thống lưu UTC, nhưng dữ liệu "theo ngày" phải gom theo ngày
/// ĐỊA PHƯƠNG. Chỉ số đo 06:45 giờ Việt Nam là 23:45 UTC hôm trước — gom
/// theo ngày UTC sẽ làm lệch biểu đồ ngày và tỉ lệ tuân thủ 30 ngày.
/// </summary>
public interface IClinicClock
{
    DateTime UtcNow { get; }
    DateTime LocalNow { get; }
    DateOnly LocalToday { get; }
    DateOnly ToLocalDate(DateTime utc);
    DateTime ToUtc(DateTime local);
    DateTime? ToLocal(DateTime? utc);
}

public class ClinicClock : IClinicClock
{
    private readonly TimeZoneInfo _tz;

    public ClinicClock(IConfiguration cfg)
    {
        var id = cfg["Clinic:TimeZone"] ?? "SE Asia Standard Time";
        try { _tz = TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException)
        {
            // Linux dùng định danh IANA, Windows dùng tên khác
            _tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }

    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);
    public DateOnly LocalToday => DateOnly.FromDateTime(LocalNow);

    public DateOnly ToLocalDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), _tz));

    public DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _tz);

    public DateTime? ToLocal(DateTime? utc)
    {
        if (utc is null) return null;
        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc),
            _tz);
    }
}
