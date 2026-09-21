namespace DiaCompanion.Api.Common;

/// <summary>Kết quả phân trang kiểu offset (QT-14) — dùng cho danh sách nông.</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

    /// <summary>Chuỗi "1–25 / 312" cho giao diện (INTERACTION.md).</summary>
    public string RangeLabel =>
        TotalItems == 0 ? "0" : $"{(Page - 1) * PageSize + 1}–{Math.Min(Page * PageSize, TotalItems)} / {TotalItems}";
}

/// <summary>
/// Phân trang kiểu keyset (QT-14) cho AuditLogs / HealthMetrics / hàng đợi triage.
/// Ngoài lợi ích tốc độ, keyset còn tránh TRƯỢT CỬA SỔ: với offset, một bản ghi mới
/// chèn vào lúc bác sĩ đang lật trang có thể làm một ca bị bỏ qua — trong worklist
/// lâm sàng đó là lỗi an toàn, không phải bất tiện.
/// </summary>
public class KeysetResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public string? NextCursor { get; set; }
    public bool HasMore => NextCursor is not null;
}

public static class Cursor
{
    public static string Encode(DateTime at, long id) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{at:O}|{id}"));

    public static (DateTime At, long Id)? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            return (DateTime.Parse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind),
                    long.Parse(parts[1]));
        }
        catch { return null; }
    }
}


public class PageQuery
{

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public string? Sort { get; set; }
    public bool Desc { get; set; }

    public int Skip => (Page - 1) * PageSize;
}