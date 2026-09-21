using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Common;
using DiaCompanion.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Services;

/// <summary>UC-18..21 — lượt khám.</summary>
public class VisitsService : BaseService, IVisitsService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IVoidService _void;
    private readonly IConfigService _cfg;
    private readonly INotificationService _notify;
    private readonly IClinicClock _clock;

    public VisitsService(IRepository repository, ICurrentUser me, IAuditService audit,
        IVoidService voidSvc, IConfigService cfg, INotificationService notify, IClinicClock clock)
    { _repository = repository; _me = me; _audit = audit; _void = voidSvc; _cfg = cfg; _notify = notify; _clock = clock; }

    public async Task<ActionResult<PagedResult<VisitDto>>> List(
        int? patientId, int? doctorId, DateOnly? from, DateOnly? to, byte? status, PageQuery page)
    {
        if (from is DateOnly fromDate && to is DateOnly toDate && fromDate > toDate)
            throw AppException.BadRequest(Msg.InvalidData, "Ngày bắt đầu không được lớn hơn ngày kết thúc.");

        DateTime? fromUtc = from is DateOnly f ? _clock.ToUtc(f.ToDateTime(TimeOnly.MinValue)) : null;
        DateTime? toExclusiveUtc = to is DateOnly t ? _clock.ToUtc(t.AddDays(1).ToDateTime(TimeOnly.MinValue)) : null;
        var data = await _repository.GetVisitPageAsync(patientId, doctorId, status, fromUtc, toExclusiveUtc, page);
        return Ok(new PagedResult<VisitDto> 
        {
            Items = data.Items.Select(ToLocalVisitDto).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalItems = data.Total
        });
    }

    public async Task<ActionResult<VisitDto>> Get(int id)
    {
        var visit = await RequireVisitDtoAsync(id);
        return Ok(ToLocalVisitDto(visit));
    }

    public async Task<ActionResult<VisitDto>> Create(CreateVisitRequest req)
    {
        var patient = await _repository.GetPatientByIdAsync(
                req.PatientId,
                tracking: false);

        if (patient is null)
        {
            throw AppException.NotFound(
                Msg.PatientNotFound,
                "Không tìm thấy hồ sơ bệnh nhân.");
        }
        //if (!await _repository.PatientExistsAsync(req.PatientId))
        //    throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");
        if (!await _repository.IsActiveUserInRoleAsync(req.DoctorId, Roles.Doctor))
            throw AppException.BadRequest(Msg.InvalidData, "Bác sĩ phụ trách không tồn tại, bị khóa hoặc role Doctor không còn active.");

        // Không cho phép bác sĩ khám chính mình.
        // Một User có thể đồng thời có role Doctor và Patient.
        // Patient.UserId chính là User.Id của người bệnh.
        if (patient.UserId is int patientUserId && patientUserId == req.DoctorId)
        {
            throw AppException.BadRequest(
                Msg.InvalidData,
                "Không thể tạo lượt khám: bác sĩ không được phụ trách lượt khám của chính mình.");
        }
        if (await _repository.HasOpenVisitAsync(req.PatientId))
            throw AppException.BadRequest(Msg.SlotTaken,
                "Bệnh nhân này đang có lượt khám chưa đóng. Vui lòng đóng lượt khám cũ trước khi tạo lượt khám mới.");

        var dayOfWeek = (byte)_clock.LocalNow.DayOfWeek;
        //if (!await _repository.IsDoctorOnDutyAsync(req.DoctorId, dayOfWeek))
        //    throw AppException.BadRequest(Msg.SlotTaken, "Bác sĩ được chọn không có ca trực tại thời điểm tiếp nhận.");
        Visit? createdVisit = null;
        //var visit = new Visit
        //{
        //    PatientId = req.PatientId,
        //    DoctorId = req.DoctorId,
        //    VisitDate = _clock.UtcNow,
        //    Status = VisitStatus.InProgress
        //};
        await _repository.ExecuteInTransactionAsync(async () =>
        {
            var medicalRecord =
            await _repository.GetActiveMedicalRecordByPatientIdAsync(
                patient.Id,
                tracking: true);
            //TH medicalRecord null thì add trước đã
            if (medicalRecord is null)
            {
                medicalRecord = new MedicalRecord
                {
                    PatientId = patient.Id,

                    // Giữ cùng format với dữ liệu migration:
                    // MR-{Patient.Code}
                    RecordCode = $"MR-{patient.Code}",

                    CreatedAt = _clock.UtcNow,

                    CreatedByUserId = _me.RequireId(),

                    IsVoided = false
                };

                _repository.Add(medicalRecord);

                // Phải save ở đây để SQL Server sinh MedicalRecord.Id.
                await _repository.CommitAsync();
            }
            // --------------------------------------------------------
            // MedicalRecord lúc này chắc chắn đã có Id.
            // Dùng Id đó làm FK cho MedicalVisit.
            // --------------------------------------------------------
            var visit = new Visit
            {

                MedicalRecordId = medicalRecord.Id,

                DoctorId = req.DoctorId,

                VisitDate = _clock.UtcNow,

                Status = VisitStatus.InProgress
            };

            _repository.Add(visit);

            await _repository.CommitAsync();

            createdVisit = visit;


            if (visit.DoctorId is int doctorId)
            {
                var patientName = await _repository.GetPatientNameAsync(visit.MedicalRecord.PatientId) ?? "bệnh nhân";
                _notify.Push(doctorId, NotificationType.Visit, "Lượt khám mới được giao",
                    $"Bạn được giao lượt khám cho {patientName}.", nameof(Visit), visit.Id);
                await _repository.CommitAsync();
            }
        });

       

        var dto = await RequireVisitDtoAsync(createdVisit.Id);

        dto.VisitDate =
            _clock.ToLocal(dto.VisitDate)!.Value;

        dto.CreatedAt =
            _clock.ToLocal(createdVisit.CreatedAt)!.Value;


        return CreatedAtAction(
            nameof(Get),
            new { id = createdVisit.Id },
            dto);
        
    }

    public async Task<ActionResult<VisitHealthMetricsDto>> GetHealthMetrics(int visitId)
    {
        var visit = await _repository.GetVisitForUpdateAsync(visitId)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy lượt khám.");

        if (visit.DoctorId != _me.RequireId())
            throw AppException.Forbidden(Msg.Forbidden,
                "Bạn không phải bác sĩ phụ trách lượt khám này.");

        var metrics = await _repository.GetVisitHealthMetricsAsync(visitId);
        return Ok(ToVisitHealthMetricsDto(visitId, metrics));
    }

    public async Task<ActionResult<VisitHealthMetricsDto>> SaveHealthMetrics(
        int visitId, SaveVisitHealthMetricsRequest req)
    {
        var visit = await _repository.GetVisitForUpdateAsync(visitId)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy lượt khám.");

        if (visit.DoctorId != _me.RequireId())
            throw AppException.Forbidden(Msg.Forbidden,
                "Bạn không phải bác sĩ phụ trách lượt khám này nên không thể nhập chỉ số.");

        if (visit.Status != VisitStatus.InProgress)
            throw AppException.Conflict(Msg.ApptImmutable,
                "Lượt khám đã đóng nên các chỉ số sức khỏe chỉ được xem, không thể chỉnh sửa.");

        ValidateVisitMetrics(req);

        var metrics = (await _repository.GetVisitHealthMetricsAsync(visitId, tracking: true)).ToList();
        var now = _clock.UtcNow;
        var localDate = _clock.ToLocalDate(now);
        var patientId = visit.MedicalRecord.PatientId;
        var patient = await _repository.GetPatientByIdAsync(patientId, tracking: false)
                    ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");
        var diabetesType = patient.DiabetesType;
        var oldValue = ToVisitHealthMetricsAudit(metrics);

        await UpsertVisitMetricAsync(
            metrics, patientId, visitId, MetricType.Glucose,
            req.Glucose, "mmol/L", req.GlucoseContext, req.GlucoseNote,
            req.GlucoseRowVersion, now, localDate, diabetesType);

        await UpsertVisitMetricAsync(
            metrics, patientId, visitId, MetricType.HbA1c,
            req.HbA1c, "%", null, req.HbA1cNote,
            req.HbA1cRowVersion, now, localDate, diabetesType);

        var systolic = metrics.FirstOrDefault(m => m.MetricType == MetricType.SystolicBp);
        var diastolic = metrics.FirstOrDefault(m => m.MetricType == MetricType.DiastolicBp);
        var bpRecordedAt = systolic?.RecordedAtUtc ?? diastolic?.RecordedAtUtc ?? now;
        var bpLocalDate = systolic?.RecordedLocalDate ?? diastolic?.RecordedLocalDate ?? localDate;

        await UpsertVisitMetricAsync(
            metrics, patientId, visitId, MetricType.SystolicBp,
            req.SystolicBp, "mmHg", null, req.BloodPressureNote,
            req.SystolicRowVersion, bpRecordedAt, bpLocalDate, diabetesType);

        await UpsertVisitMetricAsync(
            metrics, patientId, visitId, MetricType.DiastolicBp,
            req.DiastolicBp, "mmHg", null, req.BloodPressureNote,
            req.DiastolicRowVersion, bpRecordedAt, bpLocalDate, diabetesType);

        await _audit.LogAsync(
            AuditAction.MetricUpdate,
            nameof(Visit),
            visit.Id,
            oldValue,
            new
            {
                visitId = visit.Id,
                patientId,
                req.Glucose,
                glucoseContext = req.GlucoseContext?.ToString(),
                req.HbA1c,
                req.SystolicBp,
                req.DiastolicBp
            },
            "Bác sĩ cập nhật chỉ số sức khỏe trong lượt khám");

        await _repository.CommitAsync();

        var saved = await _repository.GetVisitHealthMetricsAsync(visitId);
        return Ok(ToVisitHealthMetricsDto(visitId, saved));
    }

    public async Task<ActionResult<VisitDto>> Close(int id, CloseVisitRequest req)
    {
        var visit = await _repository.GetVisitForUpdateAsync(id)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy lượt khám cần đóng.");
        _repository.ApplyOriginalRowVersion(visit, req.RowVersion);
        if (visit.Status == VisitStatus.Completed)
            throw AppException.BadRequest(Msg.ApptImmutable, "Lượt khám đã được đóng.");
        if (visit.DoctorId != _me.RequireId())
            throw AppException.Forbidden(Msg.Forbidden, "Bạn không phải bác sĩ phụ trách lượt khám này nên không thể đóng.");
        if (string.IsNullOrWhiteSpace(req.Conclusion))
            throw AppException.BadRequest(Msg.ConclusionNeeded, "Chưa nhập kết luận nên không thể đóng lượt khám.");

        var validation = await _repository.GetVisitCloseDataAsync(id);
        if (validation.PendingImages > 0)
            throw AppException.BadRequest(Msg.ConclusionNeeded,
                $"Còn {validation.PendingImages} ảnh đáy mắt chưa được duyệt chất lượng.");
        if (validation.ImagesWithoutAi > 0)
            throw AppException.BadRequest(Msg.ConclusionNeeded,
                $"Còn {validation.ImagesWithoutAi} ảnh đáy mắt đã đạt chất lượng nhưng chưa được chạy AI.");

        var withoutReview = validation.TotalAi - validation.ReviewedAi;
        if (withoutReview > 0)
            throw AppException.BadRequest(Msg.ConclusionNeeded,
                $"Còn {withoutReview}/{validation.TotalAi} kết quả AI chưa được bác sĩ phê duyệt.");

        visit.Conclusion = req.Conclusion.Trim();
        visit.Referral = req.Referral;
        visit.RecheckMonths = req.RecheckMonths
            ?? (validation.WorstGrade is byte grade
                ? await _cfg.GetRecheckMonthsAsync((DrGrade)grade)
                : (byte)12);
        visit.Status = VisitStatus.Completed;
        visit.ClosedAt = _clock.UtcNow;

        var patient = await _repository.GetPatientAsync(visit.MedicalRecord.PatientId)
            ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");

        _notify.PushToPatient(patient, NotificationType.Result, "Kết quả khám đã được xác nhận",
            $"Kết quả lượt khám ngày {_clock.ToLocal(visit.VisitDate):dd/MM/yyyy} đã được bác sĩ xác nhận.",
            nameof(Visit), visit.Id);

        var dueDate = _clock.ToLocal(visit.ClosedAt)!.Value.AddMonths(visit.RecheckMonths.Value);
        var referralNote = visit.Referral.HasValue && visit.Referral.Value >= ReferralType.Ophthalmology
            ? " Bạn cũng cần đến Khoa Mắt theo chỉ định của bác sĩ." : "";
        _notify.PushToPatient(patient, NotificationType.Recheck, "Lịch tái tầm soát tiếp theo",
            $"Đã có lịch tái tầm soát võng mạc. Vui lòng đến phòng khám trong giờ làm việc.{referralNote}",
            nameof(Visit), visit.Id);

        await _audit.LogAsync(AuditAction.VisitClose, nameof(Visit), visit.Id, null, new
        {
            visit.Conclusion,
            Referral = visit.Referral?.ToString(),
            visit.RecheckMonths,
            worstGrade = validation.WorstGrade
        });
        await _repository.CommitAsync();

        var dto = await RequireVisitDtoAsync(visit.Id);
        dto.VisitDate = _clock.ToLocal(dto.VisitDate)!.Value;
        dto.ClosedAt = _clock.ToLocal(dto.ClosedAt);
        return Ok(dto);
    }

    public async Task<IActionResult> Void(int id, VoidRequest req)
    {
        var visit = await _repository.GetVisitForUpdateAsync(id)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy lượt khám.");

        if (visit.Status == VisitStatus.Completed)
            throw AppException.Conflict(
                Msg.ApptImmutable,
                "Lượt khám đã được đóng. Hồ sơ của lượt khám này chỉ được xem và không thể chỉnh sửa hoặc thu hồi.");

        var allowedAsDoctor = _me.IsInRole(Roles.Doctor) && visit.DoctorId == _me.RequireId();
        var allowedAsReceptionist = false;
        if (_me.IsInRole(Roles.Receptionist))
        {
            if (visit.Status == VisitStatus.InProgress && !await _repository.VisitHasClinicalDataAsync(id))
                allowedAsReceptionist = true;
        }

        if (!allowedAsDoctor && !allowedAsReceptionist)
        {
            if (_me.IsInRole(Roles.Receptionist) && visit.Status != VisitStatus.InProgress)
                throw AppException.Forbidden(Msg.Forbidden, "Lượt khám đã hoàn tất, chỉ sửa hồ sơ bệnh nhân và không thu hồi");
            if (_me.IsInRole(Roles.Receptionist) && await _repository.VisitHasClinicalDataAsync(id))
                throw AppException.Forbidden(Msg.Forbidden, "Lượt khám đã có dữ liệu lâm sàng, chỉ sửa hồ sơ bệnh nhân và không thu hồi.");
            throw AppException.Forbidden(Msg.Forbidden, "Bạn không có quyền thu hồi lượt khám này.");
        }

        await _void.VoidVisitAsync(id, req.Reason, req.RowVersion);
        return Ok(new { message = "Đã thu hồi lượt khám và các bản ghi liên quan." });
    }

    public async Task<PagedResult<VisitDto>> GetMineAsync(int userId, PageQuery page)
    {
        var patientId = await RequirePatientIdForUserAsync(userId);
        var data = await _repository.GetCompletedVisitsForPatientAsync(patientId, page);
        return new PagedResult<VisitDto>
        { Items = data.Items.Select(ToLocalVisitDto).ToList(), Page = page.Page, PageSize = page.PageSize, TotalItems = data.Total };
    }

    public async Task<VisitDto> GetMineByIdAsync(int userId, int visitId)
    {
        var patientId = await RequirePatientIdForUserAsync(userId);
        var dto = await _repository.GetCompletedVisitForPatientAsync(patientId, visitId)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy lượt khám.");

        dto.VisitDate = _clock.ToLocal(dto.VisitDate)!.Value;
        dto.CreatedAt = _clock.ToLocal(dto.CreatedAt)!.Value;
        dto.ClosedAt = _clock.ToLocal(dto.ClosedAt);
        dto.HealthMetrics = ToVisitHealthMetricsDto(
            visitId, await _repository.GetVisitHealthMetricsAsync(visitId));

        // Chỉ đưa kết quả đã được bác sĩ xác nhận lên Patient. Không trả AI thô,
        // disagreement, model version hay các tín hiệu nội bộ của triage ở màn này.
        // Tái sử dụng các query read-only sẵn có; việc gọi repository này KHÔNG
        // tạo audit Export (audit chỉ nằm trong ExportService).
        var reviews = await _repository.GetVisitDiagnosisReviewsForExportAsync(visitId);
        dto.ConfirmedFindings = reviews
            .Where(r => !r.IsVoided && r.AiDiagnosis?.FundusImage is not null)
            .GroupBy(r => r.AiDiagnosis!.FundusImage!.Eye)
            .Select(group =>
            {
                // Nếu cùng một mắt có nhiều ảnh/review, hiển thị mức nặng nhất;
                // nếu cùng mức thì ưu tiên review mới hơn.
                var selected = group
                    .OrderByDescending(r => (byte)r.FinalGrade)
                    .ThenByDescending(r => r.CreatedAt)
                    .First();

                return new VisitConfirmedFindingDto
                {
                    Eye = (byte)group.Key,
                    FinalGrade = (byte)selected.FinalGrade,
                    FinalGradeLabel = DiagnosesService.GradeLabel((byte)selected.FinalGrade),
                    ConfirmedBy = selected.Doctor?.FullName,
                    ConfirmedAt = _clock.ToLocal(selected.CreatedAt)!.Value
                };
            })
            .OrderBy(x => x.Eye)
            .ToList();

        var prescriptions = await _repository.GetVisitPrescriptionsForExportAsync(visitId);
        dto.Prescriptions = prescriptions
            .Where(p => !p.IsVoided)
            .OrderBy(p => p.IssuedAt)
            .Select(p => new VisitPrescriptionDto
            {
                Id = p.Id,
                IssuedAt = _clock.ToLocal(p.IssuedAt)!.Value,
                Note = p.Note,
                Items = p.Items
                    .Where(i => i.IsActive)
                    .OrderBy(i => i.Id)
                    .Select(i => new VisitPrescriptionItemDto
                    {
                        Id = i.Id,
                        DrugName = i.DrugName,
                        Dose = i.Dose,
                        TimesPerDay = i.TimesPerDay,
                        DurationDays = i.DurationDays,
                        Instruction = i.Instruction
                    })
                    .ToList()
            })
            .ToList();

        return dto;
    }

    private async Task<int> RequirePatientIdForUserAsync(int userId)
    {
        var patientId = await _repository.GetPatientIdByUserIdAsync(userId);
        return patientId ?? throw AppException.NotFound(Msg.PatientNotFound,
            "Tài khoản chưa được liên kết với hồ sơ bệnh nhân.");
    }

    private async Task<VisitDto> RequireVisitDtoAsync(int id)
    {
        var dto = await _repository.GetVisitDtoAsync(id)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy lượt khám.");
        dto.HealthMetrics = ToVisitHealthMetricsDto(
            id, await _repository.GetVisitHealthMetricsAsync(id));
        return dto;
    }

    private static object ToVisitHealthMetricsAudit(IEnumerable<HealthMetric> rows) =>
        rows.Select(m => new
        {
            m.Id,
            type = m.MetricType.ToString(),
            m.Value,
            context = m.Context?.ToString(),
            m.Unit,
            m.Note,
            m.RecordedAtUtc
        }).ToList();

    private void ValidateVisitMetrics(SaveVisitHealthMetricsRequest req)
    {
        if (req.Glucose is decimal glucose)
        {
            if (glucose is < 1m or > 40m)
                throw AppException.BadRequest(Msg.InvalidData,
                    "Glucose phải nằm trong khoảng 1–40 mmol/L.");
            if (req.GlucoseContext is not (MetricContext.BeforeMeal or MetricContext.AfterMeal))
                throw AppException.BadRequest(Msg.RequiredFields,
                    "Glucose phải chọn thời điểm trước ăn hoặc sau ăn.");
        }

        if (req.HbA1c is decimal hba1c && (hba1c < 3m || hba1c > 20m))
            throw AppException.BadRequest(Msg.InvalidData,
                "HbA1c phải nằm trong khoảng 3–20%.");

        var hasSystolic = req.SystolicBp.HasValue;
        var hasDiastolic = req.DiastolicBp.HasValue;
        if (hasSystolic != hasDiastolic)
            throw AppException.BadRequest(Msg.RequiredFields,
                "Huyết áp phải nhập đồng thời cả tâm thu và tâm trương.");

        if (hasSystolic)
        {
            var sys = req.SystolicBp!.Value;
            var dia = req.DiastolicBp!.Value;
            if (sys is < 40m or > 300m)
                throw AppException.BadRequest(Msg.InvalidData,
                    "Huyết áp tâm thu phải nằm trong khoảng 40–300 mmHg.");
            if (dia is < 20m or > 200m)
                throw AppException.BadRequest(Msg.InvalidData,
                    "Huyết áp tâm trương phải nằm trong khoảng 20–200 mmHg.");
            if (sys <= dia)
                throw AppException.BadRequest(Msg.InvalidData,
                    "Huyết áp tâm thu phải lớn hơn huyết áp tâm trương.");
        }
    }

    private async Task UpsertVisitMetricAsync(
        List<HealthMetric> metrics,
        int patientId,
        int visitId,
        MetricType type,
        decimal? value,
        string unit,
        MetricContext? context,
        string? note,
        string? rowVersion,
        DateTime recordedAtUtc,
        DateOnly recordedLocalDate,
        byte diabetesType
        )
    {
        var metric = metrics.FirstOrDefault(m => m.MetricType == type);

        // PUT là toàn bộ trạng thái form: null => xóa mềm metric hiện có.
        if (value is null)
        {
            if (metric is not null)
            {
                _repository.ApplyOriginalRowVersion(metric, rowVersion ?? "");
                metric.IsDeleted = true;
                metric.DeletedAt = _clock.UtcNow;
            }
            return;
        }

        var abnormal = await IsVisitMetricAbnormalAsync(type, value.Value, context, diabetesType);

        if (metric is null)
        {
            metric = new HealthMetric
            {
                PatientId = patientId,
                VisitId = visitId,
                MetricType = type,
                Value = value.Value,
                Unit = unit,
                Context = context,
                RecordedAtUtc = recordedAtUtc,
                RecordedLocalDate = recordedLocalDate,
                Note = note?.Trim(),
                IsAbnormal = abnormal
            };
            _repository.Add(metric);
            metrics.Add(metric);
            return;
        }

        _repository.ApplyOriginalRowVersion(metric, rowVersion ?? "");
        metric.Value = value.Value;
        metric.Unit = unit;
        metric.Context = context;
        metric.Note = note?.Trim();
        metric.IsAbnormal = abnormal;
    }

    private async Task<bool> IsVisitMetricAbnormalAsync(
        MetricType type, decimal value, MetricContext? context, byte diabetesType)
    {
        return type switch
        {
            MetricType.Glucose =>
                GlucoseThresholds.IsAbnormal(diabetesType, value, context),
            MetricType.HbA1c =>
                value > await _cfg.GetDecimalAsync("metric.hba1c_target", 7.0m),
            MetricType.SystolicBp => value >= 140m,
            MetricType.DiastolicBp => value >= 90m,
            _ => false
        };
    }

    private static VisitHealthMetricsDto ToVisitHealthMetricsDto(
        int visitId, IReadOnlyList<HealthMetric> metrics)
    {
        var glucose = metrics.FirstOrDefault(m => m.MetricType == MetricType.Glucose);
        var hba1c = metrics.FirstOrDefault(m => m.MetricType == MetricType.HbA1c);
        var systolic = metrics.FirstOrDefault(m => m.MetricType == MetricType.SystolicBp);
        var diastolic = metrics.FirstOrDefault(m => m.MetricType == MetricType.DiastolicBp);

        HealthMetricDto? Map(HealthMetric? m) => m is null ? null : new HealthMetricDto
        {
            Id = m.Id,
            VisitId = m.VisitId,
            MetricType = (byte)m.MetricType,
            Value = m.Value,
            Unit = m.Unit,
            Context = (byte?)m.Context,
            RecordedAtUtc = m.RecordedAtUtc,
            RecordedLocalDate = m.RecordedLocalDate,
            Note = m.Note,
            IsAbnormal = m.IsAbnormal,
            RowVersion = m.ToRowVersion()
        };

        HealthMetricDto? bp = null;
        if (systolic is not null || diastolic is not null)
        {
            var primary = systolic ?? diastolic!;
            var pair = ReferenceEquals(primary, systolic) ? diastolic : systolic;
            bp = Map(primary)!;
            bp.IsAbnormal = primary.IsAbnormal || pair?.IsAbnormal == true;
            bp.PairMetricId = pair?.Id;
            bp.PairRowVersion = pair?.ToRowVersion();
            bp.SystolicValue = systolic?.Value;
            bp.DiastolicValue = diastolic?.Value;
        }

        return new VisitHealthMetricsDto
        {
            VisitId = visitId,
            Glucose = Map(glucose),
            HbA1c = Map(hba1c),
            BloodPressure = bp
        };
    }

    private VisitDto ToLocalVisitDto(VisitDto dto)
    {
        dto.VisitDate = _clock.ToLocal(dto.VisitDate)!.Value;
        dto.CreatedAt = _clock.ToLocal(dto.CreatedAt)!.Value;
        dto.ClosedAt = _clock.ToLocal(dto.ClosedAt);

        return dto;
    }
}
