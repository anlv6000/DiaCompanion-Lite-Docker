using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Repositories;

namespace DiaCompanion.Api.Services;

public interface IGradingPackageService
{
    Task<List<GradingPackageDto>> List(CancellationToken ct = default);
    Task<List<GraderAccountDto>> Graders(CancellationToken ct = default);
    Task<GradingPackageDto> Create(CreateGradingPackageRequest req, CancellationToken ct = default);
    Task<IActionResult> Download(int id, CancellationToken ct = default);
    Task<GradingPackageDto> Cancel(int id, CancellationToken ct = default);
    Task<GradingImportResultDto> Import(IFormFile file, CancellationToken ct = default);
}

/// <summary>
/// 1.3.1: chấm mù trên nhiều máy, không cần mạng.
///
/// Máy 1 (bác sĩ khám: vòng 1 + AI) là nơi giữ dữ liệu. Người quản lý nghiên cứu (vai trò Research) tạo gói
/// vòng 2 hoặc 3 cho một người chấm, tải tệp zip, chép sang máy 2 (vòng 2) hoặc máy 3 (phân xử).
/// Gói gồm ảnh đã bỏ metadata, đặt tên bằng mã ngẫu nhiên, thứ tự xáo trộn, thông tin lâm sàng ẩn danh
/// (như trang chấm trong ứng dụng) và trang chấm HTML; KHÔNG có mã bệnh nhân, kết quả AI hay phân độ của vòng trước.
/// Người chấm mở index.html bằng trình duyệt, chấm, xuất tệp JSON và gửi về; máy 1 nhập thành BlindGrade.
///
/// Quy tắc vòng chấm giữ nguyên như chấm trong ứng dụng (GradingService): vòng 2 chỉ ảnh vòng 1 chấm được,
/// người chấm không phải người chấm vòng 1; vòng 3 chỉ ảnh vòng 1 và 2 bất đồng, người phân xử không tham gia
/// vòng 1/2 của ảnh. Ảnh trong gói còn mở không vào hàng chờ trong ứng dụng hay gói khác cùng vòng.
/// </summary>
public class GradingPackageService : BaseService, IGradingPackageService
{
    public const string ResultFormat = "dr-ai-diagnosis/grading-result";
    public const int ResultFormatVersion = 1;
    private const int DefaultMaxImages = 150;
    private const string TemplateResource = "GradingPackage.index.html";
    // Bỏ các ký tự dễ đọc nhầm (0/O, 1/I/L).
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly IRepository _repository;
    private readonly IGradingService _grading;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IFileStorageService _storage;
    private readonly IClinicClock _clock;
    private readonly ILogger<GradingPackageService> _log;

    public GradingPackageService(IRepository repository, IGradingService grading, ICurrentUser me, IAuditService audit,
                                 IFileStorageService storage, IClinicClock clock, ILogger<GradingPackageService> log)
    {
        _repository = repository; _grading = grading; _me = me; _audit = audit;
        _storage = storage; _clock = clock; _log = log;
    }

    /* ---------------------------------------------------------------- danh sách */
    public async Task<List<GradingPackageDto>> List(CancellationToken ct = default) =>
        (await _repository.GetGradingPackagesAsync(ct)).Select(ToDto).ToList();

    public async Task<List<GraderAccountDto>> Graders(CancellationToken ct = default)
    {
        var list = new List<GraderAccountDto>();
        foreach (var g in await _repository.GetGraderAccountsAsync(ct))
        {
            list.Add(new GraderAccountDto
            {
                Id = g.Id,
                FullName = g.FullName,
                Email = g.Email,
                Round2Available = (await _grading.EligibleImageIdsFor(2, g.Id, ct)).Count,
                Round3Available = (await _grading.EligibleImageIdsFor(3, g.Id, ct)).Count,
            });
        }
        return list;
    }

    /* ---------------------------------------------------------------- tạo gói */
    public async Task<GradingPackageDto> Create(CreateGradingPackageRequest req, CancellationToken ct = default)
    {
        if (req.Round is not (2 or 3))
            throw AppException.BadRequest(Msg.InvalidData, "Vòng chấm phải là 2 hoặc 3.");
        var grader = await _repository.GetGraderAccountAsync(req.GraderId, ct)
            ?? throw AppException.BadRequest(Msg.InvalidData,
                "Người được chọn không có vai trò người chấm độc lập (Grader) đang hoạt động.");

        var eligible = await _grading.EligibleImageIdsFor(req.Round, grader.Id, ct);
        if (eligible.Count == 0)
            throw AppException.BadRequest(Msg.InvalidData, req.Round == 2
                ? $"Không còn ảnh nào cần chấm vòng 2 mà {grader.FullName} được chấm (ảnh đã có vòng 2, đang nằm trong gói khác, hoặc chính người này chấm vòng 1)."
                : $"Không còn ảnh bất đồng nào cần phân xử mà {grader.FullName} được chấm.");

        var max = Math.Clamp(req.MaxImages ?? DefaultMaxImages, 1, 500);
        // Xáo trộn bằng bộ sinh số ngẫu nhiên mật mã: thứ tự trong gói không suy ra được thứ tự khám.
        var chosen = eligible.ToArray();
        RandomNumberGenerator.Shuffle(chosen.AsSpan());
        chosen = chosen.Take(max).ToArray();

        var code = await NewCodeAsync(req.Round, ct);
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var package = new GradingPackage
        {
            Code = code,
            Round = req.Round,
            GraderId = grader.Id,
            Key = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
            Status = GradingPackageStatus.Open,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            CreatedBy = _me.RequireId(),
            CreatedAt = _clock.UtcNow,
        };
        for (var i = 0; i < chosen.Length; i++)
        {
            string t;
            do t = RandomToken(10); while (!tokens.Add(t));
            package.Items.Add(new GradingPackageItem { FundusImageId = chosen[i], Token = t, Position = i + 1 });
        }

        _repository.Add(package);
        await _repository.CommitAsync(ct);
        await _audit.LogAsync(AuditAction.GradingPackageCreate, nameof(GradingPackage), package.Id, null,
            new { package.Code, package.Round, package.GraderId, images = chosen.Length });
        await _repository.CommitAsync(ct);

        return ToDto(new GradingPackageSummary(package.Id, package.Code, package.Round, grader.Id, grader.FullName,
            grader.Email, package.Status, package.CreatedAt, null, package.Note, chosen.Length, 0));
    }

    /* ---------------------------------------------------------------- tải gói (zip) */
    public async Task<IActionResult> Download(int id, CancellationToken ct = default)
    {
        var package = await _repository.GetGradingPackageForUpdateAsync(id, ct)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy gói chấm.");
        if (package.Status == GradingPackageStatus.Cancelled)
            throw AppException.Conflict(Msg.InvalidData, "Gói đã hủy, không tải được nữa.");
        var grader = await _repository.GetGraderAccountAsync(package.GraderId, ct);
        var files = await _repository.GetGradingPackageFilesAsync(package.Id, ct);

        // Ghi zip ra tệp tạm (ZipArchive ghi đồng bộ; gói có thể vài trăm MB, không giữ trong RAM).
        // DeleteOnClose: tệp tự xóa khi gửi xong hoặc khi kết nối bị ngắt.
        var tmp = Path.Combine(Path.GetTempPath(), $"drai-pkg-{Guid.NewGuid():N}.zip");
        var output = new FileStream(tmp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1 << 16,
            FileOptions.DeleteOnClose);
        try
        {
            var root = package.Code + "/";
            var manifestItems = new List<object>();
            var skipped = new List<int>();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
            {
                foreach (var f in files)
                {
                    ct.ThrowIfCancellationRequested();
                    if (f.FilePath is null || !_storage.Exists(f.FilePath))
                    {
                        skipped.Add(f.Position);   // ảnh đã thu hồi hoặc mất tệp
                        continue;
                    }
                    byte[] raw;
                    await using (var src = _storage.OpenRead(f.FilePath))
                    using (var ms = new MemoryStream())
                    {
                        await src.CopyToAsync(ms, ct);
                        raw = ms.ToArray();
                    }
                    byte[] clean; string ext;
                    try { (clean, ext) = ImageMetadataStripper.Strip(raw); }
                    catch (InvalidDataException ex)
                    {
                        // Không âm thầm đưa bản gốc (có thể còn metadata định danh) vào gói.
                        _log.LogWarning(ex, "Ảnh {ImageId} trong gói {Code} không đọc được cấu trúc", f.FundusImageId, package.Code);
                        throw AppException.Conflict(Msg.InvalidData,
                            $"Ảnh số {f.Position} của gói không đúng định dạng JPEG/PNG nên không bỏ được metadata. " +
                            "Thu hồi ảnh đó (thay ảnh) hoặc hủy gói và tạo lại.");
                    }
                    var path = $"images/{f.Token}{ext}";
                    // Ảnh đã nén sẵn: lưu không nén lại cho nhanh.
                    var entry = zip.CreateEntry(root + path, CompressionLevel.NoCompression);
                    entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero); // không lộ giờ chụp/nạp
                    await using (var es = entry.Open()) await es.WriteAsync(clean, ct);
                    // 1.3.1: thông tin lâm sàng ẨN DANH như trang chấm trong ứng dụng (không mã nghiên cứu,
                    // ngày khám, năm sinh, ghi chú). Không có kết quả AI hay phân độ vòng trước.
                    var cx = await _repository.GetImageClinicalContextAsync(f.FundusImageId, ct);
                    var c = cx is null ? null : GradingService.Anonymize(GradingService.BuildClinicalContext(cx));
                    manifestItems.Add(new
                    {
                        t = f.Token, n = f.Position, f = path,
                        c = c is null ? null : new
                        {
                            eye = c.Eye, age = c.AgeAtVisit, gender = c.Gender,
                            diabetesType = c.DiabetesType, diabetesYears = c.DiabetesDurationYears,
                            hba1c = c.HbA1c, baselineHba1c = c.BaselineHbA1c,
                            sbp = c.SystolicBp, dbp = c.DiastolicBp,
                            glucose = c.Glucose, glucoseContext = c.GlucoseContext,
                        },
                    });
                }

                var data = new
                {
                    format = "dr-ai-diagnosis/grading-package",
                    formatVersion = 1,
                    app = "DR AI-Diagnosis",
                    package = package.Code,
                    key = package.Key,
                    round = package.Round,
                    graderName = grader?.FullName ?? "",
                    createdAt = _clock.ToLocal(package.CreatedAt)?.ToString("yyyy-MM-dd HH:mm"),
                    items = manifestItems,
                };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    // Giữ nguyên chữ Việt trong data.js; "<" vẫn được mã hóa nên không thể đóng thẻ script.
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                });
                await AddTextAsync(zip, root + "data.js",
                    "/* Dữ liệu gói chấm, sinh tự động bởi DR AI-Diagnosis. Không sửa tệp này. */\nwindow.DRAI_PACKAGE = " + json + ";\n", ct);
                await AddTextAsync(zip, root + "index.html", await ReadTemplateAsync(), ct);
                await AddTextAsync(zip, root + "HUONG-DAN.txt", Instructions(package, grader, manifestItems.Count), ct);
            }

            if (manifestItems.Count == 0)
                throw AppException.Conflict(Msg.InvalidData, "Mọi ảnh trong gói đã bị thu hồi. Hủy gói này.");

            await _audit.LogAsync(AuditAction.Export, nameof(GradingPackage), package.Id,
                detail: $"Tải gói chấm {package.Code}: {manifestItems.Count} ảnh" +
                        (skipped.Count > 0 ? $", bỏ {skipped.Count} ảnh đã thu hồi (số {string.Join(", ", skipped)})" : ""));
            await _repository.CommitAsync(ct);

            output.Position = 0;
            return File(output, "application/zip", $"GOI-CHAM-{package.Code}.zip");
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
    }

    /* ---------------------------------------------------------------- hủy gói */
    public async Task<GradingPackageDto> Cancel(int id, CancellationToken ct = default)
    {
        var package = await _repository.GetGradingPackageForUpdateAsync(id, ct)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy gói chấm.");
        if (package.Status != GradingPackageStatus.Open)
            throw AppException.Conflict(Msg.InvalidData, "Chỉ hủy được gói đang mở.");
        // Phân độ đã nhập giữ nguyên; ảnh chưa nhập được trả về hàng chờ (chấm trong ứng dụng hoặc gói mới).
        package.Status = GradingPackageStatus.Cancelled;
        package.ClosedAt = _clock.UtcNow;
        var imported = package.Items.Count(i => i.BlindGradeId != null);
        await _audit.LogAsync(AuditAction.GradingPackageCancel, nameof(GradingPackage), package.Id, null,
            new { package.Code, imported, released = package.Items.Count - imported });
        await _repository.CommitAsync(ct);
        return await SummaryAsync(package.Id, ct);
    }

    /* ---------------------------------------------------------------- nhập kết quả */
    public async Task<GradingImportResultDto> Import(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw AppException.BadRequest(Msg.RequiredFields, "Chọn tệp kết quả (.json) do trang chấm xuất ra.");
        if (file.Length > 10 * 1024 * 1024)
            throw AppException.BadRequest(Msg.FileTooLarge, "Tệp kết quả quá lớn (tối đa 10 MB).");

        GradingResultFile? res;
        try
        {
            await using var s = file.OpenReadStream();
            res = await JsonSerializer.DeserializeAsync<GradingResultFile>(s, cancellationToken: ct);
        }
        catch (JsonException)
        {
            throw AppException.BadRequest(Msg.InvalidData, "Tệp không phải JSON hợp lệ. Chọn đúng tệp KET-QUA-….json.");
        }
        if (res is null || res.Format != ResultFormat || res.FormatVersion != ResultFormatVersion || res.Items is null)
            throw AppException.BadRequest(Msg.InvalidData, "Tệp không phải kết quả chấm của DR AI-Diagnosis (sai định dạng).");

        var package = await _repository.GetGradingPackageByCodeForUpdateAsync(res.Package ?? "", ct)
            ?? throw AppException.BadRequest(Msg.InvalidData, $"Không có gói {res.Package} trên máy này.");
        if (res.Key is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(res.Key), Encoding.ASCII.GetBytes(package.Key)))
            throw AppException.BadRequest(Msg.InvalidData, $"Tệp không khớp gói {package.Code} (khóa gói sai).");
        if (res.Round != package.Round)
            throw AppException.BadRequest(Msg.InvalidData, "Vòng chấm trong tệp không khớp gói.");
        if (package.Status == GradingPackageStatus.Cancelled)
            throw AppException.Conflict(Msg.InvalidData, $"Gói {package.Code} đã hủy, không nhận kết quả.");
        if (!string.Equals(res.Checksum, GradingResultChecksum.Compute(res), StringComparison.OrdinalIgnoreCase))
            throw AppException.BadRequest(Msg.InvalidData,
                "Tệp kết quả bị sửa hoặc hỏng (sai mã kiểm tra). Xuất lại từ trang chấm, không mở và lưu tệp bằng trình soạn thảo.");

        var result = new GradingImportResultDto { PackageCode = package.Code, Round = package.Round, ItemsInFile = res.Items.Count };
        var byToken = package.Items.ToDictionary(i => i.Token, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var now = _clock.UtcNow;

        // Dữ liệu chấm hiện có của mọi ảnh trong gói (một lần truy vấn).
        var imageIds = package.Items.Select(i => i.FundusImageId).ToList();
        var grades = (await _repository.GetBlindGradesForImagesAsync(imageIds, ct))
            .GroupBy(g => g.FundusImageId).ToDictionary(g => g.Key, g => g.ToList());
        var active = await _repository.GetActiveImageIdsAsync(imageIds, ct);

        var toAdd = new List<(GradingPackageItem Item, BlindGrade Grade)>();
        foreach (var x in res.Items)
        {
            if (x.Token is null || !byToken.TryGetValue(x.Token, out var item))
            {
                result.Invalid.Add(new() { Reason = $"Mã ảnh \"{x.Token}\" không thuộc gói." });
                continue;
            }
            if (!seen.Add(x.Token))
            {
                result.Invalid.Add(new() { Position = item.Position, Reason = "Ảnh xuất hiện hai lần trong tệp." });
                continue;
            }
            var error = ValidateItem(x);
            if (error is not null) { result.Invalid.Add(new() { Position = item.Position, Reason = error }); continue; }
            var gradable = x.Gradable!.Value;
            DrGrade? grade = gradable ? (DrGrade)x.Grade!.Value : null;
            var note = string.IsNullOrWhiteSpace(x.Note) ? null : x.Note.Trim();

            var existing = grades.TryGetValue(item.FundusImageId, out var gl) ? gl : new List<BlindGrade>();
            if (item.BlindGradeId is int gid)
            {
                var prev = existing.FirstOrDefault(g => g.Id == gid);
                if (prev is not null && prev.Gradable == gradable && prev.Grade == grade && prev.Note == note)
                    result.Unchanged++;
                else
                    result.Conflicts.Add(new() { Position = item.Position,
                        Reason = "Đã nhập phân độ khác cho ảnh này ở lần trước. Phân độ chấm mù không sửa được; giữ bản đã nhập." });
                continue;
            }
            if (!active.Contains(item.FundusImageId))
            {
                result.Conflicts.Add(new() { Position = item.Position, Reason = "Ảnh đã bị thu hồi trên máy 1, bỏ qua." });
                continue;
            }
            var conflict = RoundConflict(package, existing);
            if (conflict is not null) { result.Conflicts.Add(new() { Position = item.Position, Reason = conflict }); continue; }

            var bg = new BlindGrade
            {
                FundusImageId = item.FundusImageId,
                Round = package.Round,
                GraderId = package.GraderId,
                Gradable = gradable,
                Grade = grade,
                Note = note,
                CreatedAt = GradedAt(x.At, package.CreatedAt, now),
                SourcePackageId = package.Id,
            };
            toAdd.Add((item, bg));
        }

        if (toAdd.Count > 0)
        {
            foreach (var (item, bg) in toAdd)
            {
                _repository.Add(bg);
                item.BlindGrade = bg;
                item.ImportedAt = now;
            }
            // Chỉ mục duy nhất (ảnh, vòng) chặn trường hợp ảnh vừa được chấm trong ứng dụng cùng lúc.
            if (!await _repository.TryCommitUniqueAsync(ct))
                throw AppException.Conflict(Msg.ConcurrentEdit,
                    "Một số ảnh vừa được chấm ở nơi khác trong lúc nhập. Nhập lại tệp này (phần đã có sẽ được bỏ qua).");
            result.Imported = toAdd.Count;
        }

        var remaining = package.Items.Count(i => i.BlindGradeId == null && i.BlindGrade == null);
        // Ảnh đã thu hồi không bao giờ nhận được kết quả: không tính là còn thiếu.
        remaining -= package.Items.Count(i => i.BlindGradeId == null && i.BlindGrade == null && !active.Contains(i.FundusImageId));
        if (remaining == 0 && package.Status == GradingPackageStatus.Open)
        {
            package.Status = GradingPackageStatus.Completed;
            package.ClosedAt = now;
        }
        result.Remaining = remaining;
        result.PackageStatus = (byte)package.Status;

        await _audit.LogAsync(AuditAction.GradingPackageImport, nameof(GradingPackage), package.Id, null, new
        {
            package.Code, package.Round, inFile = res.Items.Count, result.Imported, result.Unchanged,
            conflicts = result.Conflicts.Count, invalid = result.Invalid.Count, remaining
        });
        await _repository.CommitAsync(ct);
        return result;
    }

    /* ---------------------------------------------------------------- quy tắc */

    /// <summary>Lý do không ghi được phân độ vòng của gói cho ảnh này (null = ghi được).</summary>
    private static string? RoundConflict(GradingPackage p, List<BlindGrade> g)
    {
        var r1 = g.FirstOrDefault(x => x.Round == 1);
        var r2 = g.FirstOrDefault(x => x.Round == 2);
        var r3 = g.FirstOrDefault(x => x.Round == 3);
        if (r1 is null) return "Ảnh không còn phân độ vòng 1.";
        if (p.Round == 2)
        {
            if (r2 is not null) return "Ảnh đã có phân độ vòng 2 (người khác chấm trong ứng dụng hoặc gói khác).";
            if (!r1.Gradable) return "Vòng 1 đánh giá ảnh không chấm được nên không cần vòng 2.";
            if (r1.GraderId == p.GraderId) return "Người chấm của gói đã chấm vòng 1 ảnh này.";
            return null;
        }
        if (r3 is not null) return "Ảnh đã có phân độ vòng 3.";
        if (r2 is null) return "Ảnh chưa có vòng 2 nên chưa phân xử được.";
        if (!GradingService.Disagree(r1, r2)) return "Vòng 1 và vòng 2 không còn bất đồng, không cần phân xử.";
        if (r1.GraderId == p.GraderId || r2.GraderId == p.GraderId) return "Người phân xử đã tham gia vòng 1 hoặc 2 của ảnh này.";
        return null;
    }

    private static string? ValidateItem(GradingResultItem x)
    {
        if (x.Gradable is null) return "Thiếu thông tin chấm được/không chấm được.";
        if (x.Gradable.Value && x.Grade is null) return "Thiếu phân độ.";
        if (x.Gradable.Value && x.Grade is < 0 or > 4) return "Phân độ phải từ 0 đến 4.";
        if (!x.Gradable.Value && x.Grade is not null) return "Ảnh không chấm được thì không có phân độ.";
        if (x.Note is { Length: > 500 }) return "Ghi chú dài quá 500 ký tự.";
        return null;
    }

    /// <summary>Giờ người chấm lưu (giờ máy chấm, có thể lệch). Ngoài khoảng [tạo gói − 1 ngày, bây giờ + 10 phút] thì dùng giờ nhập.</summary>
    private static DateTime GradedAt(string? at, DateTime packageCreatedUtc, DateTime nowUtc)
    {
        if (at is not null && DateTimeOffset.TryParse(at, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var dto))
        {
            var utc = dto.UtcDateTime;
            if (utc >= packageCreatedUtc.AddDays(-1) && utc <= nowUtc.AddMinutes(10)) return utc;
        }
        return nowUtc;
    }

    /* ---------------------------------------------------------------- tiện ích */
    private async Task<string> NewCodeAsync(byte round, CancellationToken ct)
    {
        for (var i = 0; i < 20; i++)
        {
            var code = $"V{round}-{_clock.LocalNow:yyyyMMdd}-{RandomToken(4)}";
            if (!await _repository.GradingPackageCodeExistsAsync(code, ct)) return code;
        }
        throw AppException.Conflict(Msg.InvalidData, "Không sinh được mã gói, thử lại.");
    }

    private static string RandomToken(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    private static async Task AddTextAsync(ZipArchive zip, string path, string text, CancellationToken ct)
    {
        var e = zip.CreateEntry(path, CompressionLevel.Optimal);
        e.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var s = e.Open();
        // UTF-8 có BOM cho tệp .txt để Notepad cũ đọc đúng tiếng Việt; html/js khai báo charset nên không cần.
        var bom = path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
        await s.WriteAsync(new UTF8Encoding(bom).GetPreamble(), ct);
        await s.WriteAsync(Encoding.UTF8.GetBytes(text), ct);
    }

    private static async Task<string> ReadTemplateAsync()
    {
        var asm = typeof(GradingPackageService).Assembly;
        var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(TemplateResource, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Thiếu tài nguyên trang chấm (Resources/GradingPackage/index.html).");
        await using var s = asm.GetManifestResourceStream(name)!;
        using var rd = new StreamReader(s, Encoding.UTF8);
        return await rd.ReadToEndAsync();
    }

    private string Instructions(GradingPackage p, GraderAccount? g, int count)
    {
        var round = p.Round == 2 ? "VÒNG 2 - CHẤM ĐỘC LẬP" : "VÒNG 3 - PHÂN XỬ";
        return string.Join("\r\n", new[]
        {
            "DR AI-Diagnosis - GÓI CHẤM MÙ " + p.Code,
            round,
            "Người chấm: " + (g?.FullName ?? ""),
            "Số ảnh: " + count,
            "",
            "CÁCH DÙNG (không cần mạng, không cần cài đặt)",
            "1. Giải nén TOÀN BỘ tệp zip: chuột phải > Extract All. Không mở trực tiếp trong tệp zip.",
            "2. Mở tệp index.html trong thư mục vừa giải nén bằng Google Chrome hoặc Microsoft Edge.",
            "3. Chấm từng ảnh theo thang ICDR (R0 đến R4) hoặc chọn \"Không chấm được\". Kết quả tự lưu trên máy này.",
            "4. Bấm \"Xuất kết quả\": trình duyệt tải về tệp KET-QUA-" + p.Code + "-....json (thường trong thư mục Downloads).",
            "5. Gửi tệp .json đó cho người quản lý nghiên cứu (USB hoặc thư nội bộ). Có thể xuất nhiều lần.",
            "",
            "QUY ĐỊNH",
            "- Mỗi ảnh kèm thông tin lâm sàng ẩn danh (tuổi, giới, loại và số năm ĐTĐ, HbA1c, huyết áp, đường huyết)",
            "  như trang chấm trong ứng dụng. Gói không có mã bệnh nhân, kết quả AI hay phân độ của người khác.",
            "- Không trao đổi kết quả với người chấm khác. Không đổi tên, không sửa các tệp trong gói.",
            "- Xóa thư mục gói khỏi máy sau khi đã gửi kết quả và người quản lý xác nhận đã nhập.",
            "",
            "Tạo lúc: " + (_clock.ToLocal(p.CreatedAt)?.ToString("dd/MM/yyyy HH:mm") ?? ""),
        }) + "\r\n";
    }

    private async Task<GradingPackageDto> SummaryAsync(int id, CancellationToken ct) =>
        ToDto((await _repository.GetGradingPackagesAsync(ct)).First(p => p.Id == id));

    private GradingPackageDto ToDto(GradingPackageSummary s) => new()
    {
        Id = s.Id,
        Code = s.Code,
        Round = s.Round,
        GraderId = s.GraderId,
        GraderName = s.GraderName,
        GraderEmail = s.GraderEmail,
        Status = (byte)s.Status,
        CreatedAt = _clock.ToLocal(s.CreatedAt) ?? s.CreatedAt,
        ClosedAt = _clock.ToLocal(s.ClosedAt),
        Note = s.Note,
        ItemCount = s.ItemCount,
        ImportedCount = s.ImportedCount,
    };
}
