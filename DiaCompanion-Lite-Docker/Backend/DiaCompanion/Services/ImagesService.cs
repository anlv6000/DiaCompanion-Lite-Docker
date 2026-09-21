using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

/// <summary>UC-22, UC-23, UC-24, UC-26 — ảnh đáy mắt.</summary>
public class ImagesService : BaseService, IImagesService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IVoidService _void;
    private readonly IFileStorageService _storage;
    private readonly IClinicClock _clock;

    public ImagesService(IRepository repository, ICurrentUser me, IAuditService audit,
                            IVoidService voidSvc, IFileStorageService storage, IClinicClock clock)
    { _repository = repository; _me = me; _audit = audit; _void = voidSvc; _storage = storage; _clock = clock; }
    public async Task<ActionResult<List<FundusImageDto>>> List(
        [FromQuery] int? patientId, [FromQuery] int? visitId)
    {
        if (patientId is null && visitId is null)
            throw AppException.BadRequest(Msg.RequiredFields, "Cần chỉ định patientId hoặc visitId.");

        var rows = await _repository.GetFundusImagesAsync(patientId, visitId);

        var items = rows.Select(f => new FundusImageDto
        {
            Id = f.Id,
            PatientId = f.PatientId,
            VisitId = f.VisitId,
            Eye = (byte)f.Eye,
            QualityStatus = (byte)f.QualityStatus,
            QualityNote = f.QualityNote,
            CreatedAt = _clock.ToLocal(f.CreatedAt)!.Value,
            ContentUrl = $"/api/images/{f.Id}/content",
            RowVersion = f.ToRowVersion()
        }).ToList();

        return Ok(items);
    }

    /// <summary>Lấy metadata một ảnh (chất lượng, rowVersion) — độc lập với AI,
    /// để duyệt chất lượng được cả trước khi chạy AI (UC-23).</summary>
    public async Task<ActionResult<FundusImageDto>> Get(int id)
    {
        var f = await _repository.GetFundusImageAsync(id)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy ảnh.");
        EnsureCanReadPatient(_me, f.PatientId);
        return Ok(new FundusImageDto
        {
            Id = f.Id,
            PatientId = f.PatientId,
            VisitId = f.VisitId,
            Eye = (byte)f.Eye,
            QualityStatus = (byte)f.QualityStatus,
            QualityNote = f.QualityNote,
            CreatedAt = _clock.ToLocal(f.CreatedAt)!.Value,
            ContentUrl = $"/api/images/{f.Id}/content",
            RowVersion = f.ToRowVersion()
        });
    }

    /// <summary>UC-22 — nạp ảnh đáy mắt.</summary>
    [RequestSizeLimit(12 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<FundusImageDto>> Upload([FromForm] UploadFundusRequest req)
    {
        var file = req.File;
        var patientId = req.PatientId;
        var visitId = req.VisitId;
        var eye = req.Eye;


        var visit = await _repository.GetVisitForPatientAsync(req.VisitId, req.PatientId);

        if (visit is null)
            throw AppException.BadRequest(Msg.RequiredFields, "Vui lòng tải lại lượt khám!");

        if (file is null || file.Length == 0)
            throw AppException.BadRequest(Msg.RequiredFields, "Vui lòng chọn tệp ảnh.");


        if (visit.Status != VisitStatus.InProgress)
            throw AppException.Conflict(Msg.PatientNotFound, "Không thể tải ảnh vào lượt khám đã đóng.");

        var patient = await _repository.GetPatientAsync(patientId)
            ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");

        if (visitId is int vid && await _repository.GetVisitForPatientAsync(vid, patientId) is null)
            throw AppException.BadRequest(Msg.RequiredFields, "Lượt khám không thuộc bệnh nhân này.");

        await using var stream = file.OpenReadStream();
        var stored = await _storage.SaveFundusAsync(stream, file.FileName, patient.Code, visitId);

        var image = new FundusImage
        {
            PatientId = patientId,
            VisitId = visitId,
            Eye = eye,
            FilePath = stored.RelativePath,
            FileSha256 = stored.Sha256,
            SizeBytes = (int)stored.SizeBytes,
            // BR-01: mọi ảnh vào hệ thống ở trạng thái Chờ duyệt.
            // Không ảnh nào được chạy AI trước khi có người xác nhận chất lượng.
            QualityStatus = QualityStatus.Pending,
            UploadedBy = _me.RequireId()
        };

        _repository.Add(image);
        await _repository.CommitAsync();

        await _audit.LogAsync(AuditAction.ImageUpload, nameof(FundusImage), image.Id,
            null, new { patientId, visitId, eye = eye.ToString(), sha256 = stored.Sha256 });
        await _repository.CommitAsync();

        return Ok(new FundusImageDto
        {
            Id = image.Id,
            PatientId = image.PatientId,
            VisitId = image.VisitId,
            Eye = (byte)image.Eye,
            QualityStatus = (byte)image.QualityStatus,
            CreatedAt = _clock.ToLocal(image.CreatedAt)!.Value,
            ContentUrl = $"/api/images/{image.Id}/content",
            RowVersion = image.ToRowVersion()
        });
    }

    /// <summary>
    /// UC-26 — phục vụ nội dung ảnh.
    ///
    /// QT-18: file nằm ngoài webroot. Mọi lượt xem đều đi qua đây để kiểm quyền,
    /// thay vì phát tĩnh hay dùng presigned URL của dịch vụ đám mây (hệ thống
    /// triển khai tại chỗ, có thể không có internet).
    /// </summary>
    public async Task<IActionResult> Content(int id)
    {
        var image = await _repository.GetFundusImageAsync(id)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy ảnh.");

        // Kiểm tra phạm vi đọc hồ sơ.
        // User có staff role được sử dụng quyền staff.
        EnsureCanReadPatient(_me, image.PatientId);

        if (!_storage.Exists(image.FilePath))
            throw AppException.NotFound(Msg.LoadFailed, "Tệp ảnh không còn trên hệ thống.");

        var stream = _storage.OpenRead(image.FilePath);
        var contentType = Path.GetExtension(image.FilePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            _ => "image/jpeg"
        };
        return File(stream, contentType);
    }

    /// <summary>
    /// UC-23 — Bác sĩ kiểm duyệt chất lượng ảnh trước khi chạy AI.
    /// </summary>
    public async Task<IActionResult> SetQuality(int id, QualityCheckRequest req)
    {
        var image = await _repository.GetFundusImageWithVisitForUpdateAsync(id)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy ảnh.");

        var currentUserId = _me.RequireId();

        // Bác sĩ chỉ được thao tác trên lượt khám do mình phụ trách.
        if (_me.IsInRole(Roles.Doctor) &&
            image.Visit?.DoctorId != currentUserId)
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Bạn không phải bác sĩ phụ trách lượt khám này.");
        }


        if (image.Visit?.Status != VisitStatus.InProgress)
        {
            throw AppException.Conflict(
                Msg.ApptImmutable,
                "Không thể thay đổi ảnh của lượt khám đã đóng.");
        }

        var hasDiagnosis = await _repository.HasDiagnosisForImageAsync(id);

        if (hasDiagnosis)
        {
            throw AppException.Conflict(
                Msg.InvalidData,
                "Ảnh đã được chạy AI nên không thể thay đổi đánh giá chất lượng.");
        }
        _repository.ApplyOriginalRowVersion(image, req.RowVersion);

        if (req.Status == QualityStatus.Ungradable && string.IsNullOrWhiteSpace(req.Note))
            throw AppException.BadRequest(Msg.RequiredFields,
                "Vui lòng nhập lý do khi đánh dấu ảnh không đạt chất lượng.");

        var before = new { status = image.QualityStatus.ToString(), image.QualityNote };

        image.QualityStatus = req.Status;
        image.QualityNote = req.Note?.Trim();
        image.QualityCheckedBy = _me.RequireId();
        image.QualityCheckedAt = _clock.UtcNow;

        await _audit.LogAsync(AuditAction.QualityCheck, nameof(FundusImage), image.Id,
            before, new { status = req.Status.ToString(), note = req.Note });
        await _repository.CommitAsync();

        return Ok(new
        {
            message = "Đã cập nhật trạng thái chất lượng ảnh.",
            rowVersion = image.ToRowVersion()
        });
    }

    /// <summary>UC-24 — thu hồi ảnh (lan sang kết quả AI và review của ảnh đó).</summary>
    public async Task<IActionResult> Void(int id, VoidRequest req)
    {
        await _void.VoidImageAsync(id, req.Reason, req.RowVersion);
        return Ok(new { message = "Đã thu hồi ảnh và các kết quả liên quan." });
    }
}
