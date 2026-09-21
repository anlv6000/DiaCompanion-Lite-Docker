using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

/// <summary>UC-53, UC-58..61 — nghiệp vụ quản trị; không truy cập DbContext/EF.</summary>
public class AdminService : BaseService, IAdminService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IClinicClock _clock;

    public AdminService(IRepository repository, ICurrentUser me, IAuditService audit, IClinicClock clock)
    {
        _repository = repository;
        _me = me;
        _audit = audit;
        _clock = clock;
    }

    public async Task<ActionResult<DashboardDto>> Dashboard(DateOnly? from, DateOnly? to, int? modelVersionId)
    {
        var localFrom = from ?? new DateOnly(_clock.LocalNow.Year, _clock.LocalNow.Month, 1);
        var localTo = to ?? _clock.LocalToday;
        if (localTo < localFrom)
            throw AppException.BadRequest(Msg.InvalidData, "Ngày kết thúc phải từ ngày bắt đầu trở đi.");

        var fromUtc = _clock.ToUtc(localFrom.ToDateTime(TimeOnly.MinValue));
        var toExclusiveUtc = _clock.ToUtc(localTo.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var systemScope = _me.IsInRole(Roles.Admin);
        var doctorId = !systemScope && _me.IsInRole(Roles.Doctor) ? _me.RequireId() : (int?)null;
        var stats = await _repository.GetDashboardStatsAsync(
            fromUtc,
            toExclusiveUtc,
            modelVersionId,
            doctorId,
            countAllPatients: systemScope && from is null && to is null && modelVersionId is null);

        return Ok(new DashboardDto
        {
            PeriodFrom = localFrom,
            PeriodTo = localTo,
            ModelVersionId = modelVersionId,
            Scope = doctorId.HasValue ? "AssignedDoctor" : "System",
            TotalPatients = stats.TotalPatients,
            VisitsThisMonth = stats.Visits,
            PendingTriage = stats.PendingTriage,
            DeferredPending = stats.DeferredPending,
            DeferralRate = Pct(stats.DeferredTotal, stats.TotalDiagnoses),
            ReferralRate = Pct(stats.Referred, stats.ClosedVisits),
            OverrideRate = Pct(stats.Overrides, stats.TotalReviews),
            GradeDistribution = stats.GradeDistribution.ToDictionary(
                x => DiagnosesService.GradeLabel(x.Grade), x => x.Count),
            ActiveModel = stats.ActiveModel ?? "(chưa kích hoạt)"
        });
    }

    public async Task<ActionResult<List<SystemConfigDto>>> Configs()
    {
        var rows = await _repository.GetSystemConfigsAsync();
        return Ok(rows.Select(c => new SystemConfigDto
        {
            Key = c.Key,
            Value = c.Value,
            ValueType = c.ValueType,
            Description = c.Description,
            MinValue = c.MinValue,
            MaxValue = c.MaxValue,
            UpdatedAt = _clock.ToLocal(c.UpdatedAt),
            RowVersion = c.ToRowVersion()
        }).ToList());
    }

    public async Task<IActionResult> UpdateConfig(string key, UpdateConfigRequest req)
    {
        var config = await _repository.GetSystemConfigForUpdateAsync(key)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy khóa cấu hình.");
        _repository.ApplyOriginalRowVersion(config, req.RowVersion);

        if (config.ValueType is "decimal" or "int")
        {
            if (!decimal.TryParse(req.Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var number))
                throw AppException.BadRequest(Msg.ThresholdRange, "Giá trị phải là số.");
            if (config.MinValue is decimal min && number < min)
                throw AppException.BadRequest(Msg.ThresholdRange,
                    $"Giá trị phải nằm trong khoảng {config.MinValue} đến {config.MaxValue}.");
            if (config.MaxValue is decimal max && number > max)
                throw AppException.BadRequest(Msg.ThresholdRange,
                    $"Giá trị phải nằm trong khoảng {config.MinValue} đến {config.MaxValue}.");
        }

        var oldValue = config.Value;
        config.Value = req.Value;
        config.UpdatedBy = _me.RequireId();
        config.UpdatedAt = _clock.UtcNow;
        await _audit.LogAsync(AuditAction.ConfigChange, nameof(SystemConfig), null,
            new { key, value = oldValue }, new { key, value = req.Value });

        if (!await _repository.TryCommitAsync())
            throw AppException.Conflict(Msg.StaleVersion, "Cấu hình đã thay đổi. Vui lòng tải lại dữ liệu.");

        return Ok(new { message = "Cập nhật cấu hình thành công.", rowVersion = config.ToRowVersion() });
    }

    public async Task<ActionResult<ThresholdImpactDto>> ThresholdImpact(
        [FromQuery] string key,
        [FromQuery] decimal proposed)
    {
        // Chỉ còn ngưỡng bất đồng là tín hiệu quyết định. Độ tin cậy đã nghỉ hưu
        // nên không còn ước tính theo ngưỡng tin cậy nữa.
        if (key != ConfigKeys.DisagreementThreshold)
            throw AppException.BadRequest(Msg.RequiredFields,
                "Chỉ ước tính được cho ngưỡng bất đồng.");

        var currentRaw = await _repository.GetSystemConfigValueAsync(key);
        var currentValue = decimal.TryParse(currentRaw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var cv) ? cv : 0m;

        var rows = await _repository.GetDiagnosisThresholdRowsAsync();
        // Ước tính đơn giản: đếm ca có bất đồng vượt ngưỡng đề xuất. Không mô
        // phỏng phần hạ ngưỡng theo nguy cơ nền hay ca thiếu nhánh — Note đã nêu rõ.
        var projected = rows.Count(r => (r.Disagreement ?? 0) > proposed);
        var currentDeferred = rows.Count(r => r.IsDeferred);

        return Ok(new ThresholdImpactDto
        {
            CurrentThreshold = currentValue,
            ProposedThreshold = proposed,
            TotalCases = rows.Count,
            CurrentDeferred = currentDeferred,
            ProjectedDeferred = projected,
            CurrentRate = Pct(currentDeferred, rows.Count),
            ProjectedRate = Pct(projected, rows.Count),
            Note = "Ước tính trên dữ liệu lịch sử. Ngưỡng mới chỉ áp dụng cho các ca chạy sau khi lưu; kết quả đã có giữ nguyên ngưỡng tại thời điểm chạy."
        });
    }

    public async Task<ActionResult<List<ModelVersionDto>>> Models()
    {
        var models = await _repository.GetModelVersionsWithCountsAsync();
        return Ok(models.Select(x => MapModel(x.Model, x.DiagnosisCount)).ToList());
    }

    public async Task<ActionResult<ModelVersionDto>> RegisterModel(RegisterModelRequest req)
    {
        if (!Enum.IsDefined(typeof(ModelType), req.ModelType))
            throw AppException.BadRequest(Msg.InvalidData, "ModelType phải là 1=Dr, 2=Lesion hoặc 3=Fractal.");

        var sha256 = req.Sha256.Trim().ToLowerInvariant();
        if (sha256.Length != 64 || sha256.Any(c => !Uri.IsHexDigit(c)))
            throw AppException.BadRequest(Msg.InvalidData, "SHA-256 phải gồm đúng 64 ký tự hệ 16.");
        foreach (var metric in new[] { req.Qwk, req.Dice, req.IoU })
            if (metric is decimal value && (value < 0 || value > 1))
                throw AppException.BadRequest(Msg.InvalidData, "QWK, Dice và IoU phải nằm trong khoảng 0 đến 1.");
        if (req.Qwk is null && req.Dice is null && req.IoU is null)
            throw AppException.BadRequest(Msg.RequiredFields,
                "Cần nhập ít nhất một chỉ số đánh giá QWK, Dice hoặc IoU cho phiên bản mô hình.");

        var name = req.Name.Trim();
        if (await _repository.ModelNameExistsAsync(name))
            throw AppException.Conflict(Msg.InvalidData, "Tên phiên bản mô hình đã tồn tại.");

        var model = new ModelVersion
        {
            ModelType = req.ModelType,
            Name = name,
            FilePath = req.FilePath.Trim(),
            Sha256 = sha256,
            Qwk = req.Qwk,
            Dice = req.Dice,
            IoU = req.IoU,
            Note = req.Note,
            IsActive = false,
            WasActivated = false,
            CreatedBy = _me.RequireId()
        };
        _repository.Add(model);
        await _repository.CommitAsync();
        await _audit.LogAsync(AuditAction.ModelRegister, nameof(ModelVersion), model.Id,
            null, new { model.ModelType, model.Name, model.Sha256, model.Qwk, model.Dice, model.IoU });
        await _repository.CommitAsync();
        return Ok(await GetModelDtoAsync(model.Id));
    }

    public async Task<IActionResult> ActivateModel(int id, ConcurrencyRequest req)
    {
        return await _repository.ExecuteInTransactionAsync<IActionResult>(async () =>
        {
            var model = await _repository.GetModelVersionAsync(id, tracking: true)
                ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy phiên bản mô hình.");
            _repository.ApplyOriginalRowVersion(model, req.RowVersion);

            ValidateActivationMetrics(model);

            if (model.IsActive)
                return Ok(new
                {
                    message = "Phiên bản này đang được sử dụng.",
                    rowVersion = model.ToRowVersion()
                });

            var current = await _repository.GetOtherActiveModelsForUpdateAsync(
                model.Id,
                model.ModelType);

            // Tắt model cũ cùng loại và SAVE trước. Việc tách hai SaveChanges vẫn
            // nằm trong cùng transaction, nên vừa tránh va unique filtered index
            // UX_ModelVersions_ActivePerType vừa đảm bảo lỗi ở bước sau sẽ rollback.
            foreach (var activeModel in current)
                activeModel.IsActive = false;

            if (!await _repository.TryCommitAsync())
                throw AppException.Conflict(
                    Msg.StaleVersion,
                    "Model đang được sử dụng đã thay đổi. Vui lòng tải lại trước khi kích hoạt.");

            // Sau khi slot active của ModelType đã được giải phóng mới bật version mới.
            model.IsActive = true;
            model.WasActivated = true;
            model.ActivatedAt = _clock.UtcNow;

            await _audit.LogAsync(
                AuditAction.ModelActivate,
                nameof(ModelVersion),
                model.Id,
                new
                {
                    modelType = model.ModelType.ToString(),
                    active = current.FirstOrDefault()?.Name
                },
                new
                {
                    modelType = model.ModelType.ToString(),
                    active = model.Name
                });

            if (!await _repository.TryCommitAsync())
                throw AppException.Conflict(
                    Msg.StaleVersion,
                    "Trạng thái phiên bản mô hình đã thay đổi. Vui lòng tải lại trước khi kích hoạt.");

            return Ok(new
            {
                message = current.Count > 0
                    ? $"Đã thay {ModelTypeLabel(model.ModelType)} từ {current[0].Name} sang {model.Name}. Các ca chạy sau thời điểm này sẽ dùng phiên bản mới; kết quả đã lưu giữ nguyên phiên bản cũ."
                    : $"Đã kích hoạt {ModelTypeLabel(model.ModelType)}: {model.Name}. Các ca chạy sau thời điểm này sẽ dùng phiên bản này.",
                rowVersion = model.ToRowVersion()
            });
        });
    }



    private static void ValidateActivationMetrics(ModelVersion model)
    {
        switch (model.ModelType)
        {
            case ModelType.Dr when model.Qwk is null:
                throw AppException.BadRequest(
                    Msg.RequiredFields,
                    "Model DR chưa có QWK nên không thể kích hoạt.");

            case ModelType.Lesion when model.Dice is null && model.IoU is null:
                throw AppException.BadRequest(
                    Msg.RequiredFields,
                    "Model Lesion cần có Dice hoặc IoU trước khi kích hoạt.");

            case ModelType.Fractal when model.Dice is null && model.IoU is null:
                throw AppException.BadRequest(
                    Msg.RequiredFields,
                    "Model Fractal cần có Dice hoặc IoU trước khi kích hoạt.");
        }
    }

    public async Task<IActionResult> DeleteModel(int id, string rowVersion)
    {
        var model = await _repository.GetModelVersionAsync(id, tracking: true)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy phiên bản mô hình.");
        _repository.ApplyOriginalRowVersion(model, rowVersion);
        if (model.WasActivated || model.IsActive)
            throw AppException.BadRequest(Msg.ModelWasActive,
                "Không thể xóa phiên bản mô hình đã từng được kích hoạt.");
        if (await _repository.ModelHasDiagnosesAsync(id))
            throw AppException.BadRequest(Msg.ModelWasActive,
                "Phiên bản này đã sinh ra kết quả chẩn đoán nên không thể xóa.");

        _repository.Remove(model);
        await _audit.LogAsync(AuditAction.ModelDelete, nameof(ModelVersion), model.Id, new { model.Name }, null);
        if (!await _repository.TryCommitAsync())
            throw AppException.Conflict(Msg.StaleVersion, "Phiên bản mô hình đã thay đổi. Vui lòng tải lại.");
        return Ok(new { message = "Đã xóa phiên bản mô hình." });
    }

    public async Task<ActionResult<KeysetResult<AuditLogDto>>> Audit(
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] int? entityId,
        [FromQuery] int? userId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? cursor,
        [FromQuery] int size = 25)
    {
        size = size is < 1 or > 100 ? 25 : size;
        var decoded = Cursor.Decode(cursor);
        // Query string from/to được hiểu là giờ clinic; DB lưu UTC.
        var fromUtc = from.HasValue ? _clock.ToUtc(DateTime.SpecifyKind(from.Value, DateTimeKind.Unspecified)) : (DateTime?)null;
        var toUtc = to.HasValue ? _clock.ToUtc(DateTime.SpecifyKind(to.Value, DateTimeKind.Unspecified)) : (DateTime?)null;
        var page = await _repository.GetAuditPageAsync(
            action,
            entityType,
            entityId,
            userId,
            fromUtc,
            toUtc,
            decoded?.At,
            decoded?.Id,
            size);

        var items = page.Items.Select(a => new AuditLogDto
        {
            Id = a.Id,
            UserName = a.UserName,
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            OldValue = a.OldValue,
            NewValue = a.NewValue,
            Detail = a.Detail,
            IpAddress = a.IpAddress,
            CreatedAt = _clock.ToLocal(a.CreatedAt)!.Value
        }).ToList();
        var last = page.Items.LastOrDefault();
        return Ok(new KeysetResult<AuditLogDto>
        {
            Items = items,
            NextCursor = page.HasMore && last is not null ? Cursor.Encode(last.CreatedAt, last.Id) : null
        });
    }

    private static decimal Pct(int part, int total) => total == 0 ? 0 : Math.Round(part * 100m / total, 1);

    private async Task<ModelVersionDto> GetModelDtoAsync(int id)
    {
        var model = await _repository.GetModelVersionAsync(id, tracking: false)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy phiên bản mô hình.");
        var diagnosisCount = await _repository.CountDiagnosesForModelAsync(id);
        return MapModel(model, diagnosisCount);
    }

    private static string ModelTypeLabel(ModelType type) => type switch
    {
        ModelType.Dr => "DR grading",
        ModelType.Lesion => "Lesion segmentation",
        ModelType.Fractal => "Fractal/vessel",
        _ => type.ToString()
    };

    private ModelVersionDto MapModel(ModelVersion model, int diagnosisCount) => new()
    {
        Id = model.Id,
        ModelType = (byte)model.ModelType,
        ModelTypeLabel = ModelTypeLabel(model.ModelType),
        Name = model.Name,
        FilePath = model.FilePath,
        Sha256 = model.Sha256,
        Qwk = model.Qwk,
        Dice = model.Dice,
        IoU = model.IoU,
        Note = model.Note,
        IsActive = model.IsActive,
        WasActivated = model.WasActivated,
        ActivatedAt = _clock.ToLocal(model.ActivatedAt),
        DiagnosisCount = diagnosisCount,
        RowVersion = model.ToRowVersion()
    };
}
