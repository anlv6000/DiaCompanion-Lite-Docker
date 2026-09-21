using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/// <summary>
/// Tải tệp trọng số lên trước khi đăng ký phiên bản mô hình.
///
/// Model DR là ensemble nhiều fold nên Files nhận NHIỀU tệp; hai model còn lại
/// mỗi loại một tệp. Tên phiên bản dùng làm tên thư mục lưu trữ, nên phải nhập
/// trước khi tải lên.
/// </summary>
public class UploadModelRequest
{
    [EnumDataType(typeof(ModelType))]
    public ModelType ModelType { get; set; }

    [Required, MaxLength(100)] public string Name { get; set; } = "";

    [Required] public List<IFormFile> Files { get; set; } = new();
}

/// <summary>
/// Kết quả tải lên. FilePath và Sha256 được điền thẳng vào biểu mẫu đăng ký,
/// người dùng không phải tự gõ — và SHA-256 do máy chủ tính trên tệp thật.
/// </summary>
public class UploadModelResponse
{
    public string FilePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public int FileCount { get; set; }
    public long TotalBytes { get; set; }
    /// <summary>True khi nhiều tệp được gom qua manifest.json.</summary>
    public bool IsManifest { get; set; }
    public string Message { get; set; } = "";
}
