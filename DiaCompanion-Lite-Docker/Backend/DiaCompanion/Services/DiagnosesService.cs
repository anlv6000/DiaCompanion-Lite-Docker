using Microsoft.AspNetCore.Mvc;
using System.Data;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

/// <summary>UC-25, UC-27, UC-28, UC-29 — suy luận AI và diễn tiến.</summary>
public class DiagnosesService : BaseService, IDiagnosesService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IAiInferenceClient _ai;
    private readonly IDeferralService _deferral;
    private readonly IConfigService _cfg;
    private readonly IVoidService _void;
    private readonly IFileStorageService _storage;
    private readonly IClinicClock _clock;
    private readonly IClinicalRiskService _risk;
    public DiagnosesService(IRepository repository, ICurrentUser me, IAuditService audit,
                               IAiInferenceClient ai, IDeferralService deferral, IClinicalRiskService risk,
                               IConfigService cfg, IVoidService voidSvc, IFileStorageService storage, IClinicClock clock)
    { _repository = repository; _me = me; _audit = audit; _ai = ai; _deferral = deferral; _risk = risk;  _cfg = cfg; _void = voidSvc; _storage = storage; _clock = clock; }

    /// <summary>
    /// UC-25 + UC-27 + UC-28 — chạy suy luận cho một ảnh.
    ///
    /// Ba việc trong một giao dịch: hai nhánh của mô hình (phân độ + phân vùng),
    /// chỉ số fractal, và tính bất đồng chéo để quyết định có chuyển bác sĩ hay không.
    ///
    /// NT-3: KHÔNG ghi FinalGrade ở đây. Kết quả nằm ở trạng thái "chưa xác nhận"
    /// cho tới khi bác sĩ duyệt hoặc ghi đè (UC-31 / UC-32).
    /// </summary>
    public async Task<ActionResult<AiDiagnosisDto>> Run(int imageId, CancellationToken ct)
    {
        var image = await _repository.GetFundusImageWithVisitForUpdateAsync(imageId, ct)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy ảnh đáy mắt.");


        var doctorId = _me.RequireId();

        if (image.Visit?.DoctorId != doctorId)
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Bạn không phải bác sĩ phụ trách lượt khám này.");
        }
        if (image.Visit?.Status != VisitStatus.InProgress)
        {
            throw AppException.Conflict(
                Msg.ApptImmutable,
                "Lượt khám đã đóng. Kết quả AI chỉ được xem và không thể chạy lại.");
        }
        // BR-01 — van chặn bắt buộc. Chạy AI trên ảnh mờ sinh ra kết quả trông
        // hợp lệ nhưng vô nghĩa, nguy hiểm hơn là không có kết quả.
        if (image.QualityStatus != QualityStatus.Gradable)
            throw AppException.BadRequest(Msg.ImageNotGradable,
                "Ảnh chưa đạt chất lượng nên không thể chạy phân tích AI.");

        var alreadyApproved = await _repository.IsImageReviewedAsync(imageId, ct);
        if (alreadyApproved)
            throw AppException.Conflict(
                "Kết quả AI đã được phê duyệt",
                "Không thể chạy lại AI sau khi bác sĩ đã phê duyệt hoặc ghi đè kết quả.");

        var activeModels = await _repository.GetActiveModelVersionsAsync(ct);
        var drModel = activeModels.SingleOrDefault(m => m.ModelType == ModelType.Dr);
        var lesionModel = activeModels.SingleOrDefault(m => m.ModelType == ModelType.Lesion);
        var fractalModel = activeModels.SingleOrDefault(m => m.ModelType == ModelType.Fractal);

        var missing = new List<string>();
        if (drModel is null) missing.Add("DR");
        if (lesionModel is null) missing.Add("Lesion");
        if (fractalModel is null) missing.Add("Fractal");
        if (missing.Count > 0)
            throw AppException.BadRequest(Msg.AiUnavailable,
                $"Chưa kích hoạt đủ 3 model AI. Thiếu: {string.Join(", ", missing)}.");

        // Đọc ngưỡng TẠI THỜI ĐIỂM CHẠY và lưu vào bản ghi. Admin đổi ngưỡng
        // sau này không được làm thay đổi kết quả đã sinh ra (BR-17).
        var disagreeThreshold = await _cfg.GetDecimalAsync(ConfigKeys.DisagreementThreshold, 0.35m);

        // Nếu dịch vụ suy luận lỗi thì ném ra ngoài — KHÔNG tạo bản ghi rỗng
        // (E2 của UC-25).
        var result = await _ai.RunAsync(
            image.FilePath,
            drModel!.FilePath,
            lesionModel!.FilePath,
            fractalModel!.FilePath,
            // Mắt trái/phải cho Module 3: trục nasal-temporal đảo chiều giữa hai
            // mắt, nên không truyền giá trị này thì các chỉ số fractal theo vùng
            // bị bỏ qua (trả null) thay vì gán sai vùng giải phẫu.
            image.Eye == Eye.Od ? "OD" : "OS",
            ct);

        var lesionGrade = result.LesionGradeImplied is byte lg ? (DrGrade)lg : (DrGrade?)null;

        // Điểm nguy cơ nền: KHÔNG còn gate defer, chỉ LƯU và dùng XẾP ƯU TIÊN
        // hàng đợi triage (ca nguy cơ cao được bác sĩ xem trước).
        var risk = await _risk.EvaluateAsync(image.PatientId, ct);

        // Ngưỡng referable đọc tại thời điểm chạy (BR-17). Lưới an toàn: chuyển
        // bác sĩ nếu bất kỳ nhánh nào chạm referable, hoặc thiếu một nhánh.
        var referableGrade = (DrGrade)(byte)await _cfg.GetIntAsync(ConfigKeys.ReferableGrade, 2);

        var deferral = _deferral.Evaluate(
            (DrGrade)result.DrGrade, lesionGrade, disagreeThreshold, referableGrade);

        var factorsText = risk.Factors.Count == 0 ? null : string.Join(" · ", risk.Factors);
        if (factorsText is { Length: > 500 }) factorsText = factorsText[..500];

        var diagnosisId = 0;
        await _repository.ExecuteInTransactionAsync(async () =>
        {
            if (await _repository.IsImageReviewedAsync(imageId, ct))
                throw AppException.Conflict(
                    "Kết quả AI đã được phê duyệt",
                    "Không thể chạy lại AI sau khi bác sĩ đã phê duyệt hoặc ghi đè kết quả.");

            var previousRuns = await _repository.GetDiagnosesForImageForUpdateAsync(imageId, ct);
            foreach (var old in previousRuns)
            {
                await _void.VoidDiagnosisAsync(
                    old.Id,
                    $"Tự động thu hồi khi chạy lại AI cho ảnh #{imageId}.",
                    old.ToRowVersion());
            }

            var diagnosis = new AiDiagnosis
            {
                FundusImageId = image.Id,
                ModelVersionId = drModel!.Id,
                LesionModelVersionId = lesionModel!.Id,
                FractalModelVersionId = fractalModel!.Id,
                DrGrade = (DrGrade)result.DrGrade,
                Confidence = result.Confidence,
                GradeProbabilities = result.Probabilities is null
                    ? null
                    : System.Text.Json.JsonSerializer.Serialize(result.Probabilities),
                LesionGradeImplied = lesionGrade,
                LesionMaskPath = result.LesionMaskPath,
                CountMA = result.CountMA,
                CountHE = result.CountHE,
                CountEX = result.CountEX,
                CountSE = result.CountSE,
                AreaMA = result.AreaMA,
                AreaHE = result.AreaHE,
                AreaEX = result.AreaEX,
                AreaSE = result.AreaSE,
                Disagreement = deferral.Disagreement,
                IsDeferred = deferral.IsDeferred,
                DeferReason = deferral.Reason,
                DisagreementThreshold = disagreeThreshold,
                EffectiveDisagreementThreshold = deferral.EffectiveThreshold,
                ClinicalRiskScore = (byte)Math.Clamp(risk.Score, 0, 255),
                ClinicalRiskFactors = factorsText,
                FractalDimension = result.FractalDimension,
                FractalSt = result.FractalSt,
                FractalSn = result.FractalSn,
                FractalIt = result.FractalIt,
                FractalIn = result.FractalIn,
                FractalAsymmetry = result.FractalAsymmetry,
                FractalTn = result.FractalTn,
                Lacunarity = result.Lacunarity,
                VesselMaskPath = result.VesselMaskPath,
                FractalNote = result.FractalNote,
                InferenceMs = result.InferenceMs
            };

            _repository.Add(diagnosis);
            await _repository.CommitAsync(ct);
            diagnosisId = diagnosis.Id;

            await _audit.LogAsync(AuditAction.AiRun, nameof(AiDiagnosis), diagnosis.Id, null, new
            {
                imageId,
                drModel = drModel!.Name,
                lesionModel = lesionModel!.Name,
                fractalModel = fractalModel!.Name,
                grade = diagnosis.DrGrade.ToString(),
                clinicalRiskScore = diagnosis.ClinicalRiskScore,
                effectiveThreshold = diagnosis.EffectiveDisagreementThreshold,
                disagreement = diagnosis.Disagreement,
                deferred = diagnosis.IsDeferred
            });
            await _repository.CommitAsync(ct);
        }, IsolationLevel.Serializable, ct);

        return Ok(await MapAsync(diagnosisId));
    }

    /// <summary>Chi tiết một kết quả AI.</summary>
    public async Task<ActionResult<AiDiagnosisDto>> Get(int id) => Ok(await MapAsync(id));

    /// <summary>Các kết quả AI của một ảnh (gồm cả lần chạy lại).</summary>
    public async Task<ActionResult<List<AiDiagnosisDto>>> ByImage(int imageId)
    {
        var ids = await _repository.GetDiagnosisIdsByImageAsync(imageId);

        var list = new List<AiDiagnosisDto>();
        foreach (var id in ids) list.Add(await MapAsync(id));
        return Ok(list);
    }

    public Task<IActionResult> LesionMask(int id) => ResultImage(id, useLesionMask: true);

    public Task<IActionResult> FractalImage(int id) => ResultImage(id, useLesionMask: false);

    private async Task<IActionResult> ResultImage(int id, bool useLesionMask)
    {
        var diagnosis = await _repository.GetDiagnosisWithImageAsync(id)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy kết quả AI.");

        if (diagnosis.FundusImage is null)
            throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy ảnh đáy mắt liên quan.");

        EnsureCanReadPatient(
    _me,
    diagnosis.FundusImage.PatientId);

        var path = useLesionMask ? diagnosis.LesionMaskPath : diagnosis.VesselMaskPath;
        if (string.IsNullOrWhiteSpace(path))
            throw AppException.NotFound(
                Msg.LoadFailed,
                useLesionMask ? "Lần chạy AI này chưa tạo ảnh mask tổn thương." : "Lần chạy AI này chưa tạo ảnh fractal.");

        // Đường dẫn trong DB là đường dẫn TƯƠNG ĐỐI do ai_service sinh ra, tính
        // từ MASK_OUTPUT_ROOT của nó (dạng "lesion/<guid>/annotated.png").
        //
        // Trước đây chỗ này ép thêm tiền tố "ai_masks/", giả định ai_service ghi
        // vào storage/ai_masks. Thực tế MASK_OUTPUT_ROOT trỏ thẳng vào storage,
        // nên file nằm ở storage/lesion/... — tiền tố ép thêm làm mọi ảnh mask
        // trả về 404 dù file vẫn còn nguyên trên đĩa.
        //
        // Thử đúng đường dẫn đã lưu trước, rồi mới thử biến thể có tiền tố, để
        // bản ghi cũ sinh ra dưới cấu hình khác vẫn đọc được.
        var normalized = path.Replace('\\', '/').TrimStart('/');

        var candidates = normalized.StartsWith("ai_masks/", StringComparison.OrdinalIgnoreCase)
            ? new[] { normalized, normalized["ai_masks/".Length..] }
            : new[] { normalized, $"ai_masks/{normalized}" };

        var storagePath = candidates.FirstOrDefault(_storage.Exists);
        if (storagePath is null)
            throw AppException.NotFound(Msg.LoadFailed, "Tệp kết quả AI không còn trên hệ thống.");

        var stream = _storage.OpenRead(storagePath);
        var contentType = Path.GetExtension(storagePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png"
        };
        return File(stream, contentType);
    }

    /// <summary>UC-24 phần kết quả — thu hồi một kết quả AI.</summary>
    public async Task<IActionResult> Void(int id, VoidRequest req)
    {
        await _void.VoidDiagnosisAsync(id, req.Reason, req.RowVersion);
        return Ok(new { message = "Đã thu hồi kết quả AI." });
    }

    /// <summary>
    /// UC-29 — diễn tiến: ghép mức DR đã xác nhận, fractal và HbA1c trên một trục
    /// thời gian, nối biến chứng mắt với mức kiểm soát bệnh gốc.
    /// </summary>
    public Task<ActionResult<ProgressionDto>> ProgressionMine(int months = 24)
    {
        var patientId = RequireMyPatientId(_me);
        return Progression(patientId, months);
    }

    public async Task<ActionResult<ProgressionDto>> Progression(int patientId, [FromQuery] int months = 24)
    {
        EnsureCanReadPatient(_me, patientId);
        var from = _clock.UtcNow.AddMonths(-months);

        // Chỉ lấy mức đã được bác sĩ xác nhận — không đưa kết quả AI thô vào
        // biểu đồ diễn tiến (BR-13).
        var confirmed = await _repository.GetConfirmedProgressionAsync(patientId, from);
        var hba1c = await _repository.GetHba1cProgressionAsync(patientId, from);

        // Gom theo NGÀY để ba chuỗi rơi vào cùng một điểm trên biểu đồ
        var points = confirmed
            .GroupBy(x => (_clock.ToLocal(x.CreatedAt) ?? x.CreatedAt).Date)
            .Select(g => new ProgressionPoint
            {
                Date = g.Key,
                VisitId = g.Select(x => x.VisitId).FirstOrDefault(),
                // Mắt nặng hơn đại diện cho lần khám (BR-21)
                ConfirmedGrade = (byte)g.Max(x => x.Grade),
                FractalDimension = g.Average(x => x.FractalDimension)
            }).ToList();

        foreach (var h in hba1c)
        {
            var day = (_clock.ToLocal(h.RecordedAtUtc) ?? h.RecordedAtUtc).Date;
            var point = points.FirstOrDefault(p => p.Date == day);
            if (point is null) points.Add(new ProgressionPoint { Date = day, HbA1c = h.Value });
            else point.HbA1c = h.Value;
        }

        points = points.OrderBy(p => p.Date).ToList();

        // NF-09: cảnh báo khi bệnh đang xấu đi
        string? warning = null;
        var graded = points.Where(p => p.ConfirmedGrade.HasValue).ToList();
        if (graded.Count >= 2)
        {
            var first = graded.First().ConfirmedGrade!.Value;
            var last = graded.Last().ConfirmedGrade!.Value;
            if (last > first)
                warning = $"Mức bệnh võng mạc tăng từ {GradeLabel(first)} lên {GradeLabel(last)} " +
                          $"trong {months} tháng gần đây. Cân nhắc rút ngắn chu kỳ tái khám.";
        }

        return Ok(new ProgressionDto { Points = points, TrendWarning = warning });
    }

    public static string GradeLabel(byte g) => g switch
    {
        0 => "Normal",
        1 => "Mild",
        2 => "Moderate",
        3 => "Severe",
        4 => "PDR",
        _ => "?"
    };

    public static string DeferLabel(byte? r) => r switch
    {
        1 => "Độ tin cậy thấp",
        2 => "Bất đồng giữa hai nhánh cao",
        3 => "Tin cậy thấp và bất đồng cao",
        4 => "Thiếu kết quả một nhánh",
        5 => "Có dấu hiệu có thể cần chuyển tuyến",
        _ => ""
    };

    private async Task<AiDiagnosisDto> MapAsync(int id)
    {
        var d = await _repository.GetDiagnosisDetailAsync(id)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy kết quả AI.");

        var review = await _repository.GetReviewByDiagnosisAsync(id);

        return new AiDiagnosisDto
        {
            Id = d.Id,
            FundusImageId = d.FundusImageId,
            VisitId = d.FundusImage?.VisitId,
            VisitStatus = d.FundusImage?.Visit is null ? null : (byte)d.FundusImage.Visit.Status,
            Eye = (byte)(d.FundusImage?.Eye ?? 0),
            ModelVersion = d.ModelVersion?.Name ?? "",
            DrModelVersionId = d.ModelVersionId,
            DrModelVersion = d.ModelVersion?.Name ?? "",
            LesionModelVersionId = d.LesionModelVersionId,
            LesionModelVersion = d.LesionModelVersion?.Name,
            FractalModelVersionId = d.FractalModelVersionId,
            FractalModelVersion = d.FractalModelVersion?.Name,
            DrGrade = (byte)d.DrGrade,
            DrGradeLabel = GradeLabel((byte)d.DrGrade),
            ClinicalRiskScore = d.ClinicalRiskScore,
            EffectiveDisagreementThreshold = d.EffectiveDisagreementThreshold,
            ClinicalRiskFactors = d.ClinicalRiskFactors,
            LesionGradeImplied = (byte?)d.LesionGradeImplied,
            CountMA = d.CountMA,
            CountHE = d.CountHE,
            CountEX = d.CountEX,
            CountSE = d.CountSE,
            Disagreement = d.Disagreement,
            IsDeferred = d.IsDeferred,
            DeferReason = (byte?)d.DeferReason,
            DeferReasonLabel = DeferLabel((byte?)d.DeferReason),
            FractalDimension = d.FractalDimension,
            FractalSt = d.FractalSt,
            FractalSn = d.FractalSn,
            FractalIt = d.FractalIt,
            FractalIn = d.FractalIn,
            FractalAsymmetry = d.FractalAsymmetry,
            FractalTn = d.FractalTn,
            Lacunarity = d.Lacunarity,
            FractalNote = d.FractalNote,
            HasLesionMask = !string.IsNullOrWhiteSpace(d.LesionMaskPath),
            HasFractalImage = !string.IsNullOrWhiteSpace(d.VesselMaskPath),
            CreatedAt = _clock.ToLocal(d.CreatedAt)!.Value,
            // NT-3: chỉ "đã xác nhận" khi có review của bác sĩ
            IsConfirmed = review is not null,
            RowVersion = d.ToRowVersion(),
            Review = review is null ? null : new ReviewDto
            {
                Id = review.Id,
                AiDiagnosisId = review.AiDiagnosisId,
                Action = (byte)review.Action,
                ActionLabel = review.Action == ReviewAction.Approve ? "Phê duyệt" : "Ghi đè",
                FinalGrade = (byte)review.FinalGrade,
                FinalGradeLabel = GradeLabel((byte)review.FinalGrade),
                Reason = review.Reason,
                DoctorName = review.Doctor?.FullName ?? "",
                CreatedAt = _clock.ToLocal(review.CreatedAt)!.Value,
                RowVersion = review.ToRowVersion()
            }
        };
    }
}
