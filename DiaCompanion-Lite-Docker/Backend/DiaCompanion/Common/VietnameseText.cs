using System.Globalization;
using System.Text;

namespace DiaCompanion.Api.Common;

/// <summary>
/// QT-15: gõ "nguyen van an" phải khớp "Nguyễn Văn Ấn".
/// Không có xử lý này thì chức năng tìm bệnh nhân gần như vô dụng trong thực tế.
/// Giá trị được lưu sẵn vào Patients.FullNameSearch để index dùng được cho tiền tố.
/// </summary>
public static class VietnameseText
{
    public static string RemoveDiacritics(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // đ/Đ không phân rã được bằng Unicode normalization nên phải thay tay
        var s = input.Replace('đ', 'd').Replace('Đ', 'D');
        var normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    public static string Normalize(string? input) => RemoveDiacritics(input).ToLowerInvariant();
}
