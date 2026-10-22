using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DiaCompanion.Api.Dtos;

namespace DiaCompanion.Api.Services;

/// <summary>
/// 1.3.1: mã kiểm tra của tệp kết quả chấm offline: SHA-256 (hex thường) của chuỗi chuẩn, đúng như trang chấm
/// tính (Resources/GradingPackage/index.html, hàm buildResult). Phát hiện tệp bị sửa tay hoặc hỏng;
/// KHÔNG phải chữ ký mật mã (khóa gói nằm ngay trong gói người chấm giữ).
///   dòng đầu : "{package}|{key}|{round}\n"
///   mỗi mục  : "{t}|{1|0}|{grade hoặc rỗng}|{note hoặc rỗng}|{at hoặc rỗng}\n"
/// </summary>
public static class GradingResultChecksum
{
    public static string Compute(GradingResultFile r)
    {
        var sb = new StringBuilder();
        sb.Append(r.Package).Append('|').Append(r.Key).Append('|').Append(r.Round.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (var x in r.Items ?? new List<GradingResultItem>())
        {
            sb.Append(x.Token).Append('|')
              .Append(x.Gradable == true ? '1' : '0').Append('|')
              .Append(x.Grade?.ToString(CultureInfo.InvariantCulture) ?? "").Append('|')
              .Append(x.Note ?? "").Append('|')
              .Append(x.At ?? "").Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }
}
