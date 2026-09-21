using System.Security.Cryptography;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Services;

/// <summary>
/// QT-18: ảnh nằm NGOÀI webroot, không phục vụ tĩnh. Truy cập qua endpoint
/// có kiểm JWT + vai trò + quyền trên bệnh nhân đó rồi mới stream file.
///
/// Không dùng presigned URL của S3/Azure vì hệ thống triển khai tại chỗ
/// (Electron + máy chủ nội bộ phòng khám), có thể không có internet.
/// </summary>
public interface IFileStorageService
{
    Task<StoredFile> SaveFundusAsync(Stream content, string originalName, string patientCode, int visitId);
    Stream OpenRead(string relativePath);
    bool Exists(string relativePath);
}

public record StoredFile(string RelativePath, long SizeBytes, string Sha256);

public class FileStorageService : IFileStorageService
{
    private readonly string _root;
    private readonly long _maxBytes;
    private readonly string[] _allowed;

    public FileStorageService(IConfiguration cfg)
    {
        _root = Path.GetFullPath(cfg["Storage:FundusRoot"] ?? "storage/fundus");
        _maxBytes = cfg.GetValue<long?>("Storage:MaxUploadBytes") ?? 10 * 1024 * 1024;
        _allowed = cfg.GetSection("Storage:AllowedExtensions").Get<string[]>()
                   ?? new[] { ".jpg", ".jpeg", ".png" };
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveFundusAsync(Stream content, string originalName, string patientCode, int visitId)
    {
        var ext = Path.GetExtension(originalName).ToLowerInvariant();
        if (!_allowed.Contains(ext))
            throw AppException.BadRequest(Msg.BadFileType, "Định dạng tệp không hợp lệ. Chỉ chấp nhận JPG hoặc PNG.");

        // Đọc vào bộ nhớ để tính checksum trước khi ghi đĩa — tránh để lại
        // tệp rác khi tải lên lỗi giữa chừng (E3 của UC-22).
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        if (ms.Length > _maxBytes)
            throw AppException.BadRequest(Msg.FileTooLarge, "Tệp ảnh vượt quá dung lượng cho phép (10 MB).");
        if (ms.Length == 0)
            throw AppException.BadRequest(Msg.BadFileType, "Tệp ảnh rỗng.");

        ms.Position = 0;
        var sha = Convert.ToHexString(await SHA256.HashDataAsync(ms)).ToLowerInvariant();
        ms.Position = 0;

        var year = DateTime.UtcNow.Year;
        var relDir = Path.Combine("fundus", year.ToString(), Sanitize(patientCode));
        var fileName = $"v{visitId}_{Guid.NewGuid():N}{ext}";
        var relPath = Path.Combine(relDir, fileName).Replace('\\', '/');

        var absDir = Path.Combine(_root, "..", relDir);
        Directory.CreateDirectory(Path.GetFullPath(absDir));

        var absPath = Path.GetFullPath(Path.Combine(_root, "..", relPath));
        await using (var fs = File.Create(absPath)) await ms.CopyToAsync(fs);

        return new StoredFile(relPath, ms.Length, sha);
    }

    public Stream OpenRead(string relativePath)
    {
        var abs = Resolve(relativePath);
        if (!File.Exists(abs))
            throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy tệp ảnh.");
        return File.OpenRead(abs);
    }

    public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

    /// <summary>
    /// Chặn path traversal: đường dẫn lưu trong CSDL vẫn phải kiểm tra lại,
    /// vì "../../appsettings.json" cũng là một chuỗi hợp lệ về mặt kiểu dữ liệu.
    /// </summary>
    private string Resolve(string relativePath)
    {
        var baseDir = Path.GetFullPath(Path.Combine(_root, ".."));
        var full = Path.GetFullPath(Path.Combine(baseDir, relativePath));
        if (!full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            throw AppException.Forbidden(Msg.Forbidden, "Đường dẫn tệp không hợp lệ.");
        return full;
    }

    private static string Sanitize(string s) =>
        string.Concat(s.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
}
