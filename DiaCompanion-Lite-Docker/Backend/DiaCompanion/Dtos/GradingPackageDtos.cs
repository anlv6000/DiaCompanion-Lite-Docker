using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DiaCompanion.Api.Dtos;

/* 1.3.1: gói chấm mù cho máy khác (chấm offline bằng trình duyệt). */

public class CreateGradingPackageRequest
{
    /// <summary>2 = người chấm độc lập; 3 = phân xử.</summary>
    [Range(2, 3, ErrorMessage = "Vòng chấm phải là 2 hoặc 3.")]
    public byte Round { get; set; }

    /// <summary>Tài khoản vai trò Grader đại diện người chấm ở máy kia.</summary>
    public int GraderId { get; set; }

    /// <summary>Số ảnh tối đa trong gói (mặc định 150, tối đa 500). Gói lớn thì tệp zip lớn.</summary>
    [Range(1, 500, ErrorMessage = "Số ảnh mỗi gói từ 1 đến 500.")]
    public int? MaxImages { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class GradingPackageDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public byte Round { get; set; }
    public int GraderId { get; set; }
    public string GraderName { get; set; } = "";
    public string? GraderEmail { get; set; }
    /// <summary>0 đang mở, 1 đã nhập đủ, 2 đã hủy.</summary>
    public byte Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Note { get; set; }
    public int ItemCount { get; set; }
    public int ImportedCount { get; set; }
}

public class GraderAccountDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    /// <summary>Số ảnh người này còn được chấm ở vòng 2 / vòng 3 (đã trừ ảnh nằm trong gói còn mở).</summary>
    public int Round2Available { get; set; }
    public int Round3Available { get; set; }
}

/// <summary>Tệp kết quả do trang chấm offline xuất ra (định dạng dr-ai-diagnosis/grading-result, phiên bản 1).</summary>
public class GradingResultFile
{
    [JsonPropertyName("format")] public string? Format { get; set; }
    [JsonPropertyName("formatVersion")] public int FormatVersion { get; set; }
    [JsonPropertyName("package")] public string? Package { get; set; }
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("round")] public int Round { get; set; }
    [JsonPropertyName("exportedAt")] public string? ExportedAt { get; set; }
    [JsonPropertyName("items")] public List<GradingResultItem>? Items { get; set; }
    [JsonPropertyName("checksum")] public string? Checksum { get; set; }
}

public class GradingResultItem
{
    /// <summary>Mã ảnh trong gói.</summary>
    [JsonPropertyName("t")] public string? Token { get; set; }
    [JsonPropertyName("gradable")] public bool? Gradable { get; set; }
    [JsonPropertyName("grade")] public int? Grade { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
    /// <summary>Thời điểm người chấm lưu phân độ (ISO 8601, giờ máy chấm).</summary>
    [JsonPropertyName("at")] public string? At { get; set; }
}

public class GradingImportIssueDto
{
    /// <summary>Số thứ tự ảnh trong gói (người chấm thấy số này), null nếu không xác định.</summary>
    public int? Position { get; set; }
    public string Reason { get; set; } = "";
}

public class GradingImportResultDto
{
    public string PackageCode { get; set; } = "";
    public byte Round { get; set; }
    public int ItemsInFile { get; set; }
    /// <summary>Phân độ mới được ghi.</summary>
    public int Imported { get; set; }
    /// <summary>Đã nhập trước đó với cùng nội dung: bỏ qua.</summary>
    public int Unchanged { get; set; }
    /// <summary>Không ghi được vì mâu thuẫn với dữ liệu đã có (đã nhập khác nội dung, ảnh đã có người chấm vòng này...).</summary>
    public List<GradingImportIssueDto> Conflicts { get; set; } = new();
    /// <summary>Mục không hợp lệ trong tệp (mã ảnh lạ, thiếu phân độ...).</summary>
    public List<GradingImportIssueDto> Invalid { get; set; } = new();
    /// <summary>Số ảnh trong gói vẫn chưa có kết quả sau lần nhập này.</summary>
    public int Remaining { get; set; }
    public byte PackageStatus { get; set; }
}
