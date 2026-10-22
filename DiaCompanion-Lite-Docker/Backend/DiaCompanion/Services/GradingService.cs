using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Repositories;

namespace DiaCompanion.Api.Services;

/// <summary>
/// Chấm mù phục vụ nghiên cứu.
/// - Vòng 1: bác sĩ khám chấm ảnh TRƯỚC khi AI chạy (DiagnosesService.Run chặn nếu chưa có).
/// - Vòng 2: người chấm độc lập, thấy ảnh + thông tin lâm sàng ẩn danh (không thấy AI, mã bệnh nhân, nhãn vòng 1).
/// - Vòng 3: người phân xử, chỉ các ảnh vòng 1 và 2 bất đồng ở ngưỡng chuyển tuyến
///   (phân độ ≥ 2) hoặc về khả năng chấm được.
/// Mọi lần chấm là bất biến và được ghi nhật ký kiểm toán.
/// </summary>
public class GradingService : BaseService, IGradingService
{
    public const string AuditBlindGrade = "BLIND_GRADE";

    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IFileStorageService _storage;
    private readonly IClinicClock _clock;

    public GradingService(IRepository repository, ICurrentUser me, IAuditService audit,
                          IFileStorageService storage, IClinicClock clock)
    { _repository = repository; _me = me; _audit = audit; _storage = storage; _clock = clock; }

    /* ------------------------------------------------------------ vòng 1 */
    public async Task<BlindGradeDto> SubmitRound1(int imageId, BlindGradeRequest req, CancellationToken ct = default)
    {
        Validate(req);
        var doctorId = _me.RequireId();
        var image = await _repository.GetFundusImageWithVisitForUpdateAsync(imageId, ct)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy ảnh đáy mắt.");

        if (image.Visit?.DoctorId != doctorId)
            throw AppException.Forbidden(Msg.Forbidden, "Bạn không phải bác sĩ phụ trách lượt khám này.");
        if (image.Visit?.Status != VisitStatus.InProgress)
            throw AppException.Conflict(Msg.ApptImmutable, "Lượt khám đã đóng, không thể chấm thêm.");
        if (await _repository.GetBlindGradeAsync(imageId, 1, ct) is not null)
            throw AppException.Conflict(Msg.InvalidData, "Ảnh này đã được chấm mù. Phân độ chấm mù không sửa được.");
        // Tính mù chỉ bảo đảm khi AI chưa từng chạy trên ảnh.
        if (await _repository.ImageHasAiDiagnosisAsync(imageId, ct))
            throw AppException.Conflict(Msg.InvalidData,
                "Ảnh đã có kết quả AI nên không còn chấm mù được. Hãy thay ảnh (nạp lại) nếu cần đưa vào nghiên cứu.");

        var grade = NewGrade(imageId, 1, doctorId, req);
        _repository.Add(grade);

        // Không chấm được ⇒ ảnh không đạt (AI không chạy, theo BR-01).
        // Chấm được và ảnh còn chờ duyệt ⇒ đánh dấu đạt để chạy AI ngay.
        var before = image.QualityStatus;
        if (!req.Gradable)
        {
            image.QualityStatus = QualityStatus.Ungradable;
            image.QualityNote = "Bác sĩ chấm mù: không chấm được";
        }
        else if (image.QualityStatus == QualityStatus.Pending)
        {
            image.QualityStatus = QualityStatus.Gradable;
        }
        if (image.QualityStatus != before)
        {
            image.QualityCheckedBy = doctorId;
            image.QualityCheckedAt = DateTime.UtcNow;
        }

        await SaveAsync(ct);
        await _audit.LogAsync(AuditBlindGrade, nameof(FundusImage), imageId, null,
            new { round = 1, req.Gradable, req.Grade });
        return ToDto(grade);
    }

    public async Task<BlindGradeDto?> GetRound1(int imageId, CancellationToken ct = default)
    {
        var g = await _repository.GetBlindGradeAsync(imageId, 1, ct);
        return g is null ? null : ToDto(g);
    }

    /* ------------------------------------------------------- vòng 2 và 3 */
    public async Task<GradingQueueDto> Queue(byte round, CancellationToken ct = default)
    {
        var ids = await EligibleImageIds(round, _me.RequireId(), ct);
        return new GradingQueueDto
        {
            Round = round,
            Remaining = ids.Count,
            Items = ids.Select((id, i) => new GradingQueueItemDto { ImageId = id, Position = i + 1 }).ToList()
        };
    }

    public async Task<IActionResult> Content(int imageId, CancellationToken ct = default)
    {
        // Người chấm chỉ xem được ảnh đã vào bộ nghiên cứu (đã có vòng 1).
        if (await _repository.GetBlindGradeAsync(imageId, 1, ct) is null)
            throw AppException.Forbidden(Msg.Forbidden, "Ảnh này không thuộc bộ chấm mù.");
        var image = await _repository.GetFundusImageAsync(imageId, false, ct)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy ảnh.");
        if (!_storage.Exists(image.FilePath))
            throw AppException.NotFound(Msg.LoadFailed, "Tệp ảnh không còn trên hệ thống.");

        var contentType = Path.GetExtension(image.FilePath).ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg";
        return File(_storage.OpenRead(image.FilePath), contentType);
    }

    public async Task<BlindGradeDto> SubmitGrader(byte round, int imageId, BlindGradeRequest req, CancellationToken ct = default)
    {
        if (round is not (2 or 3))
            throw AppException.BadRequest(Msg.InvalidData, "Vòng chấm phải là 2 hoặc 3.");
        Validate(req);
        var graderId = _me.RequireId();

        var eligible = await EligibleImageIds(round, graderId, ct);
        if (!eligible.Contains(imageId))
            throw AppException.Conflict(Msg.InvalidData,
                "Ảnh này không còn trong hàng chờ của bạn (đã có người chấm, hoặc bạn đã chấm ảnh này ở vòng khác).");

        var grade = NewGrade(imageId, round, graderId, req);
        _repository.Add(grade);
        await SaveAsync(ct);
        await _audit.LogAsync(AuditBlindGrade, nameof(FundusImage), imageId, null,
            new { round, req.Gradable, req.Grade });
        return ToDto(grade);
    }

    /// <summary>
    /// Thông tin lâm sàng của ảnh. Bác sĩ khám/Admin: đầy đủ. Người chấm vòng 2/3 (1.3.1, theo yêu cầu
    /// nghiên cứu: chấm trong cùng điều kiện thông tin với bác sĩ vòng 1): bản ẩn danh, chỉ ảnh thuộc bộ chấm mù.
    /// </summary>
    public async Task<ImageClinicalContextDto> ClinicalContext(int imageId, CancellationToken ct = default)
    {
        var full = _me.IsInRole(Roles.Doctor, Roles.Admin);
        if (!full)
        {
            if (!_me.IsInRole(Roles.Grader))
                throw AppException.Forbidden(Msg.Forbidden, "Không có quyền xem thông tin lâm sàng.");
            // Như ảnh: người chấm chỉ xem được ảnh đã vào bộ nghiên cứu (đã có vòng 1).
            if (await _repository.GetBlindGradeAsync(imageId, 1, ct) is null)
                throw AppException.Forbidden(Msg.Forbidden, "Ảnh này không thuộc bộ chấm mù.");
        }
        var c = await _repository.GetImageClinicalContextAsync(imageId, ct)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy ảnh.");
        var dto = BuildClinicalContext(c);
        return full ? dto : Anonymize(dto);
    }

    /// <summary>Bản cho người chấm vòng 2/3 và gói chấm offline: bỏ mã nghiên cứu, ngày khám, năm sinh
    /// và ghi chú tự do (có thể chứa thông tin định danh). Giữ tuổi, giới, loại/số năm ĐTĐ, HbA1c, HA, đường huyết.</summary>
    public static ImageClinicalContextDto Anonymize(ImageClinicalContextDto d)
    {
        d.ResearchCode = null;
        d.VisitDate = null;
        d.BirthYear = 0;
        d.PatientNote = null;
        return d;
    }

    public static ImageClinicalContextDto BuildClinicalContext(ImageClinicalContext c)
    {
        var imageId = c.Image.Id;
        decimal? Latest(MetricType t) => c.Metrics.FirstOrDefault(m => m.MetricType == t)?.Value;
        var glucose = c.Metrics.FirstOrDefault(m => m.MetricType == MetricType.Glucose);
        var at = c.VisitDate ?? c.Image.CreatedAt;
        var dob = c.Patient.DateOfBirth;
        int? age = dob.Year > 1900 ? at.Year - dob.Year - (at.Month < dob.Month || (at.Month == dob.Month && at.Day < dob.Day) ? 1 : 0) : null;

        return new ImageClinicalContextDto
        {
            ImageId = imageId,
            Eye = (byte)c.Image.Eye,
            ResearchCode = c.Patient.FullName,
            VisitDate = c.VisitDate,
            BirthYear = dob.Year,
            AgeAtVisit = age,
            Gender = c.Patient.Gender,
            DiabetesType = c.Patient.DiabetesType,
            DiabetesDurationYears = c.Patient.DiabetesDurationYears,
            BaselineHbA1c = c.Patient.BaselineHbA1c,
            PatientNote = c.Patient.Note,
            HbA1c = Latest(MetricType.HbA1c),
            SystolicBp = Latest(MetricType.SystolicBp),
            DiastolicBp = Latest(MetricType.DiastolicBp),
            Glucose = glucose?.Value,
            GlucoseContext = glucose?.Context is MetricContext mc ? (byte)mc : null,
        };
    }

    public async Task<GradingProgressDto> Progress(CancellationToken ct = default)
    {
        var rows = await _repository.GetGradingExportRowsAsync(ct);
        var p = new GradingProgressDto();
        foreach (var r in rows)
        {
            var r1 = r.Grades.FirstOrDefault(g => g.Round == 1);
            var r2 = r.Grades.FirstOrDefault(g => g.Round == 2);
            var r3 = r.Grades.FirstOrDefault(g => g.Round == 3);
            if (r1 is null) continue;
            p.Round1++;
            if (!r1.Gradable) p.Round1Ungradable++;
            if (r2 is not null) p.Round2Done++;
            else if (r1.Gradable) p.Round2Pending++;
            if (r2 is not null && Disagree(r1, r2)) p.Round3Needed++;
            if (r3 is not null) p.Round3Done++;
        }
        p.Round2InPackages = (await _repository.GetImageIdsReservedByOpenPackagesAsync(2, ct)).Count;
        p.Round3InPackages = (await _repository.GetImageIdsReservedByOpenPackagesAsync(3, ct)).Count;
        return p;
    }

    /* ------------------------------------------------------------ helpers */
    /// <summary>Bất đồng cần phân xử: khác về chấm được/không, hoặc khác phía ngưỡng chuyển tuyến (≥ 2).</summary>
    public static bool Disagree(BlindGrade a, BlindGrade b) =>
        a.Gradable != b.Gradable ||
        (a.Gradable && b.Gradable && Referable(a.Grade) != Referable(b.Grade));

    public static bool Referable(DrGrade? g) => g is not null && (byte)g.Value >= 2;

    /// <summary>
    /// Nhãn tham chiếu cuối: vòng 3 nếu có; nếu vòng 1 và 2 không bất đồng thì lấy mức nặng hơn;
    /// còn lại (thiếu vòng 2, hoặc bất đồng chưa phân xử) = null.
    /// </summary>
    public static (bool? Gradable, DrGrade? Grade) Reference(IReadOnlyList<BlindGrade> grades)
    {
        var r1 = grades.FirstOrDefault(g => g.Round == 1);
        var r2 = grades.FirstOrDefault(g => g.Round == 2);
        var r3 = grades.FirstOrDefault(g => g.Round == 3);
        if (r3 is not null) return (r3.Gradable, r3.Grade);
        if (r1 is null || r2 is null || Disagree(r1, r2)) return (null, null);
        if (!r1.Gradable) return (false, null);
        var worst = (byte)Math.Max((byte)r1.Grade!.Value, (byte)r2.Grade!.Value);
        return (true, (DrGrade)worst);
    }

    /// <summary>Ảnh người chấm này được chấm ở vòng 2/3 (đã loại ảnh đang nằm trong gói còn mở).
    /// Dùng chung cho hàng chờ trong ứng dụng và cho việc tạo gói chấm offline.</summary>
    public Task<List<int>> EligibleImageIdsFor(byte round, int graderId, CancellationToken ct = default) =>
        EligibleImageIds(round, graderId, ct);

    private async Task<List<int>> EligibleImageIds(byte round, int graderId, CancellationToken ct)
    {
        IEnumerable<GradingCandidate> items = round switch
        {
            2 => await _repository.GetRound2CandidatesAsync(graderId, ct),
            3 => (await _repository.GetRound3PoolAsync(graderId, ct))
                    .Where(x => Disagree(x.R1, x.R2)).Select(x => x.Image),
            _ => throw AppException.BadRequest(Msg.InvalidData, "Vòng chấm phải là 2 hoặc 3.")
        };
        // 1.3.1: ảnh đã giao cho máy khác (gói chấm còn mở, chưa nhập) không vào hàng chờ trong ứng dụng,
        // tránh hai người chấm cùng một ảnh ở cùng vòng.
        var reserved = await _repository.GetImageIdsReservedByOpenPackagesAsync(round, ct);
        // Thứ tự xáo trộn cố định theo Id: người chấm không suy ra được thứ tự khám hay bệnh nhân.
        return items
            .Where(x => !reserved.Contains(x.ImageId))
            .Select(x => x.ImageId)
            .Distinct()
            .OrderBy(id => unchecked((uint)id * 2654435761u))
            .ToList();
    }

    private static void Validate(BlindGradeRequest req)
    {
        if (req.Gradable && req.Grade is null)
            throw AppException.BadRequest(Msg.RequiredFields, "Chọn phân độ DR, hoặc đánh dấu ảnh không chấm được.");
        if (!req.Gradable && req.Grade is not null)
            throw AppException.BadRequest(Msg.InvalidData, "Ảnh không chấm được thì không nhập phân độ.");
        if (req.Grade is > 4)
            throw AppException.BadRequest(Msg.InvalidData, "Phân độ phải từ 0 đến 4.");
    }

    private static BlindGrade NewGrade(int imageId, byte round, int graderId, BlindGradeRequest req) => new()
    {
        FundusImageId = imageId,
        Round = round,
        GraderId = graderId,
        Gradable = req.Gradable,
        Grade = req.Gradable ? (DrGrade)req.Grade!.Value : null,
        Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
        CreatedAt = DateTime.UtcNow
    };

    private async Task SaveAsync(CancellationToken ct)
    {
        // Chỉ mục duy nhất (ảnh, vòng) là chốt chặn cuối khi hai người lưu cùng lúc.
        if (!await _repository.TryCommitAsync(ct))
            throw AppException.Conflict(Msg.ConcurrentEdit, "Ảnh này vừa được chấm bởi người khác. Tải lại hàng chờ.");
    }

    private BlindGradeDto ToDto(BlindGrade g) => new()
    {
        ImageId = g.FundusImageId,
        Round = g.Round,
        Gradable = g.Gradable,
        Grade = g.Grade is null ? null : (byte)g.Grade.Value,
        GradeLabel = g.Grade is null ? null : DiagnosesService.GradeLabel((byte)g.Grade.Value),
        Note = g.Note,
        CreatedAt = _clock.ToLocal(g.CreatedAt) ?? g.CreatedAt
    };
}
