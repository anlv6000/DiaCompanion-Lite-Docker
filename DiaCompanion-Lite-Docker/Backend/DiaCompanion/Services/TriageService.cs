using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

/// <summary>UC-30..33 — hàng đợi triage và quyết định của bác sĩ.</summary>
public class TriageService : BaseService, ITriageService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IVoidService _void;
    private readonly IConfigService _cfg;
    private readonly IClinicClock _clock;

    public TriageService(IRepository repository, ICurrentUser me, IAuditService audit,
                            IVoidService voidSvc, IConfigService cfg, IClinicClock clock)
    { _repository = repository; _me = me; _audit = audit; _void = voidSvc; _cfg = cfg; _clock = clock; }

    /// <summary>
    /// UC-30 — hàng đợi các ca đã có kết quả AI nhưng chưa ai xác nhận.
    ///
    /// Thứ tự ưu tiên: ca bị gắn cờ chuyển bác sĩ lên trước, rồi tới ca cần
    /// chuyển tuyến, rồi theo mức bất đồng giảm dần. Bác sĩ mở màn hình là
    /// thấy ngay ca đáng ngờ nhất.
    ///
    /// Dùng KEYSET pagination chứ không offset: hàng đợi cập nhật liên tục,
    /// mà offset bị trượt cửa sổ khi có bản ghi mới chèn vào giữa lúc lật trang
    /// — bác sĩ có thể BỎ SÓT một ca. Trong worklist lâm sàng đó là lỗi an toàn.
    /// </summary>
    public async Task<ActionResult<KeysetResult<TriageItemDto>>> Queue(
        [FromQuery] int? doctorId,
        [FromQuery] bool? deferredOnly,
        [FromQuery] string? q,
        [FromQuery] string? cursor,
        [FromQuery] int size = 25)
    {
        size = size is < 1 or > 100 ? 25 : size;
        var referableGrade = (byte)await _cfg.GetIntAsync(ConfigKeys.ReferableGrade, 2);
        var decoded = Cursor.Decode(cursor);
        int? currentDoctorId = _me.IsInRole(Roles.Doctor) ? _me.RequireId() : null;
        int? filterDoctorId = currentDoctorId is null && _me.IsInRole(Roles.Admin) ? doctorId : null;
        var data = await _repository.GetTriageQueueAsync(
            currentDoctorId, filterDoctorId, deferredOnly, q, decoded?.At, decoded?.Id, size);

        var items = data.Items.Select(x => new TriageItemDto
        {
            AiDiagnosisId = x.Id, PatientId = x.PatientId, PatientCode = x.PatientCode,
            PatientName = x.PatientName, VisitId = x.VisitId, Eye = (byte)x.Eye,
            DrGrade = (byte)x.DrGrade,
            ClinicalRiskScore = x.ClinicalRiskScore,
            Disagreement = x.Disagreement,
            IsDeferred = x.IsDeferred, DeferReason = (byte?)x.DeferReason,
            NeedsReferral = (byte)x.DrGrade >= referableGrade, CreatedAt = _clock.ToLocal(x.CreatedAt)!.Value,
            DoctorName = x.DoctorName, RowVersion = x.RowVer.Length == 0 ? null : Convert.ToBase64String(x.RowVer)
        }).ToList();
        var last = data.Items.LastOrDefault();
        return Ok(new KeysetResult<TriageItemDto>
        {
            Items = items,
            NextCursor = data.HasMore && last is not null ? Cursor.Encode(last.CreatedAt, last.Id) : null
        });
    }

    /// <summary>Số ca đang chờ, để hiện badge trên thanh điều hướng.</summary>
    public async Task<IActionResult> Count()
    {
        var doctorId = _me.IsInRole(Roles.Doctor) ? _me.RequireId() : (int?)null;
        var counts = await _repository.GetTriageCountsAsync(doctorId);
        return Ok(new { pending = counts.Pending, deferred = counts.Deferred });
    }

    /// <summary>
    /// UC-31 — bác sĩ phê duyệt kết quả AI.
    /// FinalGrade = phân độ của AI, nhưng chủ thể quyết định vẫn là con người (NT-3).
    /// </summary>
    public async Task<ActionResult<ReviewDto>> Approve(int diagnosisId, ReviewRequest req)
    {
        var d = await LoadForReviewAsync(diagnosisId, req.RowVersion);

        var doctorId = _me.RequireId();
        var review = new DiagnosisReview
        {
            AiDiagnosisId = d.Id,
            DoctorId = doctorId,
            Action = ReviewAction.Approve,
            FinalGrade = d.DrGrade,
            Reason = null
        };

        // Bắt buộc cập nhật chính AiDiagnosis để RowVer thực sự tham gia câu UPDATE.
        // Nếu hai bác sĩ cùng dùng một token cũ, một request sẽ nhận 409.
        d.LastReviewActionBy = doctorId;
        d.LastReviewActionAt = _clock.UtcNow;

        _repository.Add(review);
        await _audit.LogAsync(AuditAction.ReviewApprove, nameof(AiDiagnosis), d.Id,
            new { aiGrade = (byte)d.DrGrade, confidence = d.Confidence, disagreement = d.Disagreement },
            new { finalGrade = (byte)review.FinalGrade, action = "Approve" });

        await SaveWithConcurrencyCheckAsync();
        return Ok(await MapReviewAsync(review.Id));
    }

    /// <summary>
    /// UC-32 — bác sĩ ghi đè kết quả AI.
    /// BR-04: bắt buộc có lý do. Ca này vào tập dữ liệu người–máy mâu thuẫn (UC-35).
    /// </summary>
    public async Task<ActionResult<ReviewDto>> Override(int diagnosisId, OverrideRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Reason))
            throw AppException.BadRequest(Msg.OverrideReason, "Vui lòng nhập lý do trước khi ghi đè kết quả.");

        var d = await LoadForReviewAsync(diagnosisId, req.RowVersion);

        var doctorId = _me.RequireId();
        var review = new DiagnosisReview
        {
            AiDiagnosisId = d.Id,
            DoctorId = doctorId,
            Action = ReviewAction.Override,
            FinalGrade = req.FinalGrade,
            Reason = req.Reason.Trim()
        };

        d.LastReviewActionBy = doctorId;
        d.LastReviewActionAt = _clock.UtcNow;

        _repository.Add(review);

        // Ghi lại đầy đủ tín hiệu tại thời điểm chạy: đây chính là dữ liệu
        // dùng để đánh giá cơ chế deferral có bắt đúng ca khó hay không.
        await _audit.LogAsync(AuditAction.ReviewOverride, nameof(AiDiagnosis), d.Id,
            new
            {
                aiGrade = (byte)d.DrGrade,
                confidence = d.Confidence,
                disagreement = d.Disagreement,
                wasDeferred = d.IsDeferred
            },
            new { finalGrade = (byte)req.FinalGrade, action = "Override" },
            req.Reason.Trim());

        await SaveWithConcurrencyCheckAsync();
        return Ok(await MapReviewAsync(review.Id));
    }

    /// <summary>
    /// UC-33 — thu hồi review đã lập sai.
    /// Ca tự động quay lại hàng đợi vì unique index chỉ tính review chưa void.
    /// </summary>
    public async Task<IActionResult> VoidReview(int reviewId, VoidRequest req)
    {
        var rowVersion = await _void.VoidReviewAsync(reviewId, req.Reason, req.RowVersion);
        return Ok(new
        {
            message = "Đã thu hồi bản ghi duyệt. Ca quay lại hàng đợi triage.",
            rowVersion
        });
    }

    /// <summary>
    /// Nạp kết quả AI và kiểm tra tương tranh (QT-9).
    ///
    /// Hai bác sĩ cùng mở một ca trong hàng đợi là tình huống thật, vì triage
    /// là màn hình dùng chung. Không kiểm tra thì sẽ có hai review cho một kết quả,
    /// hoặc người sau ghi đè người trước mà không để lại dấu vết.
    /// </summary>
    private async Task<AiDiagnosis> LoadForReviewAsync(int diagnosisId, string? rowVersion)
    {
        var d = await _repository.GetDiagnosisForReviewAsync(diagnosisId)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy kết quả AI.");

        var doctorId = _me.RequireId();
        var visit = d.FundusImage?.Visit;
        if (visit is null || visit.IsVoided || visit.DoctorId != doctorId)
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Bác sĩ chỉ được duyệt kết quả thuộc lượt khám do mình phụ trách.");

        if (visit.Status != VisitStatus.InProgress)
            throw AppException.Conflict(
                Msg.ApptImmutable,
                "Lượt khám đã đóng. Kết quả đã trở thành dữ liệu chỉ đọc.");

        if (await _repository.ReviewExistsForDiagnosisAsync(diagnosisId))
            throw AppException.Conflict(Msg.ConcurrentEdit,
                "Ca này vừa được một bác sĩ khác xử lý. Vui lòng tải lại hàng đợi.");

        _repository.ApplyOriginalRowVersion(d, rowVersion);
        return d;
    }

    private async Task SaveWithConcurrencyCheckAsync()
    {
        if (!await _repository.TryCommitReviewAsync())
            throw AppException.Conflict(Msg.ConcurrentEdit,
                "Ca này vừa được bác sĩ khác xử lý. Vui lòng tải lại hàng đợi.");
    }

    private async Task<ReviewDto> MapReviewAsync(int reviewId)
    {
        var r = await _repository.GetReviewAsync(reviewId)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy bản ghi duyệt.");

        return new ReviewDto
        {
            Id = r.Id,
            AiDiagnosisId = r.AiDiagnosisId,
            Action = (byte)r.Action,
            ActionLabel = r.Action == ReviewAction.Approve ? "Phê duyệt" : "Ghi đè",
            FinalGrade = (byte)r.FinalGrade,
            FinalGradeLabel = DiagnosesService.GradeLabel((byte)r.FinalGrade),
            Reason = r.Reason,
            DoctorName = r.Doctor?.FullName ?? "",
            CreatedAt = _clock.ToLocal(r.CreatedAt)!.Value,
            RowVersion = r.ToRowVersion()
        };
    }
}
