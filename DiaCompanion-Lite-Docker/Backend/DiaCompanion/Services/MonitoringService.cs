using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Common;
using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Services;

/// <summary>
/// UC-41..47 — chỉ số sức khỏe, lối sống và xác nhận dùng thuốc.
/// Đây là dữ liệu bệnh nhân tự nhập nên dùng SOFT DELETE, không phải void.
/// </summary>
public class MonitoringService : BaseService, IMonitoringService
{
    private const decimal SystolicBpAbnormalFrom = 140m;
    private const decimal DiastolicBpAbnormalFrom = 90m;

    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IClinicClock _clock;
    private readonly IConfigService _cfg;
    private readonly IAuditService _audit;

    public MonitoringService(
        IRepository repository,
        ICurrentUser me,
        IClinicClock clock,
        IConfigService cfg,
        IAuditService audit)
    {
        _repository = repository;
        _me = me;
        _clock = clock;
        _cfg = cfg;
        _audit = audit;
    }

    /* ------------------------------ CHỈ SỐ ------------------------------ */

    /// <summary>UC-41/42/43 — danh sách chỉ số và dữ liệu dùng để vẽ biểu đồ.</summary>
    public async Task<ActionResult<KeysetResult<HealthMetricDto>>> Metrics(
        [FromQuery] int? patientId, [FromQuery] MetricType? type,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? cursor, [FromQuery] int size = 50)
    {
        var pid = ResolvePatientId(_me, patientId);
        size = size is < 1 or > 200 ? 50 : size;

        var decoded = Cursor.Decode(cursor);
        DateTime? cursorAt = null;
        long? cursorId = null;
        if (decoded is (DateTime at, long lastId))
        {
            cursorAt = at;
            cursorId = lastId;
        }

        var page = await _repository.GetHealthMetricPageAsync(
            pid, type, from, to, cursorAt, cursorId, size);
        var rows = page.Items;
        var hasMore = page.HasMore;

        // Ghép cặp huyết áp ở tầng nghiệp vụ; Repository chỉ chịu trách nhiệm truy vấn dữ liệu.
        var bpByTime = page.BloodPressureRows
            .GroupBy(m => (m.VisitId, m.RecordedAtUtc))
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = rows.Select(m =>
        {
            HealthMetric? pair = null;
            if (m.MetricType is MetricType.SystolicBp or MetricType.DiastolicBp
                && bpByTime.TryGetValue((m.VisitId, m.RecordedAtUtc), out var sameTime))
            {
                var pairType = m.MetricType == MetricType.SystolicBp
                    ? MetricType.DiastolicBp
                    : MetricType.SystolicBp;
                pair = sameTime.FirstOrDefault(x => x.MetricType == pairType);
            }

            var systolic = m.MetricType == MetricType.SystolicBp
                ? m
                : pair?.MetricType == MetricType.SystolicBp ? pair : null;
            var diastolic = m.MetricType == MetricType.DiastolicBp
                ? m
                : pair?.MetricType == MetricType.DiastolicBp ? pair : null;

            return new HealthMetricDto
            {
                Id = m.Id,
                VisitId = m.VisitId,
                MetricType = (byte)m.MetricType,
                Value = m.Value,
                Unit = m.Unit,
                Context = (byte?)m.Context,
                RecordedAtUtc = _clock.ToLocal(m.RecordedAtUtc).Value,
                RecordedLocalDate = m.RecordedLocalDate,
                Note = m.Note,
                IsAbnormal = m.IsAbnormal || pair?.IsAbnormal == true,
                RowVersion = m.ToRowVersion(),
                PairMetricId = pair?.Id,
                PairRowVersion = pair?.ToRowVersion(),
                SystolicValue = systolic?.Value,
                DiastolicValue = diastolic?.Value
            };
        }).ToList();

        var last = rows.LastOrDefault();
        return Ok(new KeysetResult<HealthMetricDto>
        {
            Items = items,
            NextCursor = hasMore && last is not null ? Cursor.Encode(last.RecordedAtUtc, last.Id) : null
        });
    }

    /// <summary>UC-41 — ghi glucose, HbA1c hoặc một cặp huyết áp.</summary>
    public async Task<ActionResult<HealthMetricDto>> CreateMetric(CreateMetricRequest req)
    {
        var pid = RequireMyPatientId(_me);
        var recordedUtc = req.RecordedAtUtc ?? _clock.UtcNow;
        var diabetesType = await GetPatientDiabetesTypeAsync(pid);
        if (recordedUtc > _clock.UtcNow)
            throw AppException.BadRequest(Msg.RequiredFields, "Không thể ghi chỉ số ở thời điểm tương lai.");

        if (req.MetricType == MetricType.HbA1c)
        {
            if (req.Value is < 3m or > 20m)
                throw AppException.BadRequest(Msg.InvalidData, "HbA1c phải nằm trong khoảng 3–20%.");

            var hba1c = new HealthMetric
            {
                PatientId = pid,
                MetricType = MetricType.HbA1c,
                Value = req.Value,
                Unit = "%",
                Context = null,
                RecordedAtUtc = recordedUtc,
                RecordedLocalDate = _clock.ToLocalDate(recordedUtc),
                Note = req.Note?.Trim(),
                IsAbnormal = await IsAbnormalAsync(MetricType.HbA1c, req.Value, null, diabetesType)
            };

            _repository.Add(hba1c);
            await _repository.CommitAsync();
            await _audit.LogAsync(
                AuditAction.MetricCreate,
                nameof(HealthMetric),
                hba1c.Id,
                null,
                new { type = hba1c.MetricType.ToString(), hba1c.Value, hba1c.Unit, hba1c.RecordedAtUtc });
            await _repository.CommitAsync();
            return Ok(ToMetricDto(hba1c));
        }

        if (req.MetricType == MetricType.DiastolicBp)
            throw AppException.BadRequest(Msg.RequiredFields,
                "Huyết áp phải được nhập đồng thời cả tâm thu và tâm trương.");

        if (req.MetricType == MetricType.SystolicBp)
        {
            if (req.SystolicValue is null || req.DiastolicValue is null)
                throw AppException.BadRequest(Msg.RequiredFields,
                    "Vui lòng nhập đầy đủ huyết áp tâm thu và tâm trương.");

            var systolicValue = req.SystolicValue.Value;
            var diastolicValue = req.DiastolicValue.Value;

            if (systolicValue is < 40m or > 300m)
                throw AppException.BadRequest(Msg.RequiredFields,
                    "Huyết áp tâm thu phải nằm trong khoảng 40–300 mmHg.");

            if (diastolicValue is < 20m or > 200m)
                throw AppException.BadRequest(Msg.RequiredFields,
                    "Huyết áp tâm trương phải nằm trong khoảng 20–200 mmHg.");

            if (systolicValue <= diastolicValue)
                throw AppException.BadRequest(Msg.RequiredFields,
                    "Huyết áp tâm thu phải lớn hơn huyết áp tâm trương.");

            var recordedLocalDate = _clock.ToLocalDate(recordedUtc);

            var systolicMetric = new HealthMetric
            {
                PatientId = pid,
                MetricType = MetricType.SystolicBp,
                Value = systolicValue,
                Unit = "mmHg",
                Context = null,
                RecordedAtUtc = recordedUtc,
                RecordedLocalDate = recordedLocalDate,
                Note = req.Note,
                IsAbnormal = systolicValue >= SystolicBpAbnormalFrom
            };

            var diastolicMetric = new HealthMetric
            {
                PatientId = pid,
                MetricType = MetricType.DiastolicBp,
                Value = diastolicValue,
                Unit = "mmHg",
                Context = null,
                RecordedAtUtc = recordedUtc,
                RecordedLocalDate = recordedLocalDate,
                Note = req.Note,
                IsAbnormal = diastolicValue >= DiastolicBpAbnormalFrom
            };

            _repository.Add(systolicMetric);
            _repository.Add(diastolicMetric);
            await _repository.CommitAsync();
            await _audit.LogAsync(
                AuditAction.MetricCreate,
                nameof(HealthMetric),
                systolicMetric.Id,
                null,
                new
                {
                    type = "BloodPressure",
                    systolicId = systolicMetric.Id,
                    diastolicId = diastolicMetric.Id,
                    systolic = systolicMetric.Value,
                    diastolic = diastolicMetric.Value,
                    systolicMetric.RecordedAtUtc
                });
            await _repository.CommitAsync();

            return Ok(new HealthMetricDto
            {
                Id = systolicMetric.Id,
                VisitId = systolicMetric.VisitId,
                MetricType = (byte)systolicMetric.MetricType,
                Value = systolicMetric.Value,
                Unit = systolicMetric.Unit,
                Context = null,
                RecordedAtUtc = systolicMetric.RecordedAtUtc,
                RecordedLocalDate = systolicMetric.RecordedLocalDate,
                Note = systolicMetric.Note,
                IsAbnormal = systolicMetric.IsAbnormal || diastolicMetric.IsAbnormal,
                RowVersion = systolicMetric.ToRowVersion(),
                PairMetricId = diastolicMetric.Id,
                PairRowVersion = diastolicMetric.ToRowVersion(),
                SystolicValue = systolicMetric.Value,
                DiastolicValue = diastolicMetric.Value
            });
        }

        if (req.MetricType != MetricType.Glucose)
            throw AppException.BadRequest(Msg.RequiredFields, "Loại chỉ số không hợp lệ.");

        if (req.Context is not (MetricContext.BeforeMeal or MetricContext.AfterMeal))
            throw AppException.BadRequest(Msg.RequiredFields,
                "Thời điểm đo đường huyết phải là trước ăn hoặc sau ăn.");

        if (req.Value is < 1m or > 40m)
            throw AppException.BadRequest(Msg.RequiredFields,
                "Giá trị phải nằm trong khoảng 1–40 mmol/L.");

        var metric = new HealthMetric
        {
            PatientId = pid,
            MetricType = MetricType.Glucose,
            Value = req.Value,
            Unit = "mmol/L",
            Context = req.Context,
            RecordedAtUtc = recordedUtc,
            RecordedLocalDate = _clock.ToLocalDate(recordedUtc),
            Note = req.Note,
            IsAbnormal = await IsAbnormalAsync(MetricType.Glucose, req.Value, req.Context, diabetesType)
        };

        _repository.Add(metric);
        await _repository.CommitAsync();
        await _audit.LogAsync(
            AuditAction.MetricCreate,
            nameof(HealthMetric),
            metric.Id,
            null,
            new { type = metric.MetricType.ToString(), metric.Value, metric.Unit, metric.Context, metric.RecordedAtUtc });
        await _repository.CommitAsync();

        return Ok(new HealthMetricDto
        {
            Id = metric.Id,
            VisitId = metric.VisitId,
            MetricType = (byte)metric.MetricType,
            Value = metric.Value,
            Unit = metric.Unit,
            Context = (byte?)metric.Context,
            RecordedAtUtc = metric.RecordedAtUtc,
            RecordedLocalDate = metric.RecordedLocalDate,
            Note = metric.Note,
            IsAbnormal = metric.IsAbnormal,
            RowVersion = metric.ToRowVersion()
        });
    }
    /// <summary>UC-42 — sửa chỉ số đã nhập với optimistic concurrency.</summary>
    public async Task<IActionResult> UpdateMetric(int id, CreateMetricRequest req)
    {

        var m = await _repository.GetHealthMetricForUpdateAsync(id)
            ?? throw AppException.NotFound(
                Msg.LoadFailed,
                "Không tìm thấy bản ghi.");

        EnsureOwnPatient(_me, m.PatientId);
        var diabetesType = await GetPatientDiabetesTypeAsync(m.PatientId);
        if (m.VisitId is not null)
            throw AppException.Forbidden(Msg.Forbidden,
                "Chỉ số này được bác sĩ ghi trong lượt khám nên bệnh nhân không được sửa.");
        _repository.ApplyOriginalRowVersion(m, req.RowVersion);
        var oldMetric = new
        {
            type = m.MetricType.ToString(),
            m.Value,
            m.Context,
            m.RecordedAtUtc,
            m.Note
        };

        if (m.MetricType == MetricType.HbA1c)
        {
            if (req.MetricType != MetricType.HbA1c || req.Value is < 3m or > 20m)
                throw AppException.BadRequest(Msg.InvalidData, "HbA1c phải nằm trong khoảng 3–20%.");

            if (req.RecordedAtUtc.HasValue)
            {
                var recordedUtc = DateTime.SpecifyKind(req.RecordedAtUtc.Value, DateTimeKind.Utc);
                if (recordedUtc > _clock.UtcNow)
                    throw AppException.BadRequest(Msg.InvalidData, "Không thể ghi chỉ số ở thời điểm tương lai.");
                m.RecordedAtUtc = recordedUtc;
                m.RecordedLocalDate = _clock.ToLocalDate(recordedUtc);
            }

            m.Value = req.Value;
            m.Note = req.Note?.Trim();
            m.IsAbnormal = await IsAbnormalAsync(MetricType.HbA1c, req.Value, null, diabetesType);
            await _audit.LogAsync(
                AuditAction.MetricUpdate,
                nameof(HealthMetric),
                m.Id,
                oldMetric,
                new { type = m.MetricType.ToString(), m.Value, m.Context, m.RecordedAtUtc, m.Note });
            await _repository.CommitAsync();
            return Ok(ToMetricDto(m));
        }

        if (m.MetricType is MetricType.SystolicBp or MetricType.DiastolicBp)
        {
            if (req.MetricType != MetricType.SystolicBp)
                throw AppException.BadRequest(Msg.RequiredFields,
                    "Huyết áp phải được sửa bằng metricType tâm thu và nhập đủ tâm thu, tâm trương.");

            if (req.SystolicValue is null || req.DiastolicValue is null)
                throw AppException.BadRequest(Msg.RequiredFields,
                    "Vui lòng nhập đầy đủ huyết áp tâm thu và tâm trương.");

            var systolicValue = req.SystolicValue.Value;
            var diastolicValue = req.DiastolicValue.Value;

            if (systolicValue is < 40m or > 300m)
                throw AppException.BadRequest(Msg.RequiredFields,
                    "Huyết áp tâm thu phải nằm trong khoảng 40–300 mmHg.");

            if (diastolicValue is < 20m or > 200m)
                throw AppException.BadRequest(Msg.RequiredFields,
                    "Huyết áp tâm trương phải nằm trong khoảng 20–200 mmHg.");

            if (systolicValue <= diastolicValue)
                throw AppException.BadRequest(Msg.RequiredFields,
                    "Huyết áp tâm thu phải lớn hơn huyết áp tâm trương.");

            var systolicMetric = m.MetricType == MetricType.SystolicBp
                ? m
                : await FindBloodPressurePairAsync(m, MetricType.SystolicBp);

            var diastolicMetric = m.MetricType == MetricType.DiastolicBp
                ? m
                : await FindBloodPressurePairAsync(m, MetricType.DiastolicBp);

            var pairMetric = ReferenceEquals(m, systolicMetric) ? diastolicMetric : systolicMetric;
            _repository.ApplyOriginalRowVersion(pairMetric, req.PairRowVersion);

            var recordedUtc = m.RecordedAtUtc;
            var recordedLocalDate = m.RecordedLocalDate;

            if (req.RecordedAtUtc.HasValue)
            {
                recordedUtc = DateTime.SpecifyKind(req.RecordedAtUtc.Value, DateTimeKind.Utc);
                if (recordedUtc > _clock.UtcNow)
                    throw AppException.BadRequest(Msg.RequiredFields, "Không thể ghi chỉ số ở thời điểm tương lai.");

                recordedLocalDate = _clock.ToLocalDate(recordedUtc);
            }

            systolicMetric.Value = systolicValue;
            systolicMetric.Note = req.Note;
            systolicMetric.RecordedAtUtc = recordedUtc;
            systolicMetric.RecordedLocalDate = recordedLocalDate;
            systolicMetric.IsAbnormal = systolicValue >= SystolicBpAbnormalFrom;

            diastolicMetric.Value = diastolicValue;
            diastolicMetric.Note = req.Note;
            diastolicMetric.RecordedAtUtc = recordedUtc;
            diastolicMetric.RecordedLocalDate = recordedLocalDate;
            diastolicMetric.IsAbnormal = diastolicValue >= DiastolicBpAbnormalFrom;

            await _audit.LogAsync(
                AuditAction.MetricUpdate,
                nameof(HealthMetric),
                systolicMetric.Id,
                oldMetric,
                new
                {
                    type = "BloodPressure",
                    systolicId = systolicMetric.Id,
                    diastolicId = diastolicMetric.Id,
                    systolic = systolicMetric.Value,
                    diastolic = diastolicMetric.Value,
                    systolicMetric.RecordedAtUtc
                });
            await _repository.CommitAsync();

            return Ok(new HealthMetricDto
            {
                Id = systolicMetric.Id,
                VisitId = systolicMetric.VisitId,
                MetricType = (byte)systolicMetric.MetricType,
                Value = systolicMetric.Value,
                Unit = systolicMetric.Unit,
                Context = null,
                RecordedAtUtc = systolicMetric.RecordedAtUtc,
                RecordedLocalDate = systolicMetric.RecordedLocalDate,
                Note = systolicMetric.Note,
                IsAbnormal = systolicMetric.IsAbnormal || diastolicMetric.IsAbnormal,
                RowVersion = systolicMetric.ToRowVersion(),
                PairMetricId = diastolicMetric.Id,
                PairRowVersion = diastolicMetric.ToRowVersion(),
                SystolicValue = systolicMetric.Value,
                DiastolicValue = diastolicMetric.Value
            });
        }

        if (req.MetricType != MetricType.Glucose)
            throw AppException.BadRequest(Msg.RequiredFields, "Loại chỉ số không hợp lệ.");

        if (req.Context is not (MetricContext.BeforeMeal or MetricContext.AfterMeal))
            throw AppException.BadRequest(Msg.RequiredFields,
                "Thời điểm đo đường huyết phải là trước ăn hoặc sau ăn.");

        if (req.Value is < 1m or > 40m)
            throw AppException.BadRequest(Msg.RequiredFields,
                "Giá trị phải nằm trong khoảng 1–40 mmol/L.");

        if (req.RecordedAtUtc.HasValue)
        {
            var recordedUtc = DateTime.SpecifyKind(req.RecordedAtUtc.Value, DateTimeKind.Utc);
            if (recordedUtc > _clock.UtcNow)
                throw AppException.BadRequest(Msg.RequiredFields, "Không thể ghi chỉ số ở thời điểm tương lai.");

            m.RecordedAtUtc = recordedUtc;
            m.RecordedLocalDate = _clock.ToLocalDate(recordedUtc);
        }

        m.Value = req.Value;
        m.Context = req.Context;
        m.Note = req.Note;
        m.IsAbnormal = await IsAbnormalAsync(m.MetricType, req.Value, req.Context, diabetesType);

        await _audit.LogAsync(
            AuditAction.MetricUpdate,
            nameof(HealthMetric),
            m.Id,
            oldMetric,
            new { type = m.MetricType.ToString(), m.Value, m.Context, m.RecordedAtUtc, m.Note });
        await _repository.CommitAsync();

        return Ok(new HealthMetricDto
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
        });
    }

    /// <summary>
    /// UC-42 — xoá chỉ số. XOÁ MỀM: bản ghi ẩn khỏi biểu đồ nhưng vẫn nằm trong
    /// CSDL để bác sĩ đối chiếu nếu cần (QT-5).
    /// </summary>
    public async Task<IActionResult> DeleteMetric(int id, ConcurrencyRequest req)
    {
        var m = await _repository.GetHealthMetricForUpdateAsync(id)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy bản ghi.");
        EnsureOwnPatient(_me, m.PatientId);
        if (m.VisitId is not null)
            throw AppException.Forbidden(Msg.Forbidden,
                "Chỉ số này thuộc hồ sơ lượt khám nên bệnh nhân không được xóa.");
        _repository.ApplyOriginalRowVersion(m, req.RowVersion);

        var deletedAt = _clock.UtcNow;
        m.IsDeleted = true;
        m.DeletedAt = deletedAt;

        if (m.MetricType is MetricType.SystolicBp or MetricType.DiastolicBp)
        {
            var oppositeType = m.MetricType == MetricType.SystolicBp
                ? MetricType.DiastolicBp
                : MetricType.SystolicBp;

            var pair = await FindBloodPressurePairAsync(m, oppositeType);
            _repository.ApplyOriginalRowVersion(pair, req.PairRowVersion);
            pair.IsDeleted = true;
            pair.DeletedAt = deletedAt;
        }

        await _audit.LogAsync(
            AuditAction.MetricDelete,
            nameof(HealthMetric),
            m.Id,
            new { type = m.MetricType.ToString(), m.Value, m.RecordedAtUtc, isDeleted = false },
            new { isDeleted = true, deletedAt });
        await _repository.CommitAsync();

        return Ok(new { message = "Đã ẩn bản ghi. Dữ liệu vẫn được lưu cho bác sĩ đối chiếu." });
    }
    private async Task<HealthMetric> FindBloodPressurePairAsync(HealthMetric metric, MetricType pairType)
    {
        return await _repository.GetBloodPressurePairForUpdateAsync(
                   metric.PatientId, metric.VisitId, metric.RecordedAtUtc, pairType)
               ?? throw AppException.BadRequest(Msg.LoadFailed,
                   "Không tìm thấy đủ cặp huyết áp tâm thu và tâm trương.");
    }


    /// <summary>UC-43 — tóm tắt để vẽ biểu đồ xu hướng.</summary>
    public async Task<ActionResult<MetricSummaryDto>> Summary(int patientId, [FromQuery] int days = 30)
    {
        EnsureCanReadPatient(_me, patientId);

        if (days is < 1 or > 365)
            throw AppException.BadRequest(Msg.RequiredFields, "Khoảng thời gian xem biểu đồ phải từ 1 đến 365 ngày.");

        var today = _clock.LocalToday;
        var from = today.AddDays(-(days - 1));

        var metrics = await _repository.GetHealthMetricsAsync(patientId, from, today);
        var diabetesType = await GetPatientDiabetesTypeAsync(patientId);

        var glucose = metrics.Where(m => m.MetricType == MetricType.Glucose).ToList();
        var hba1c = metrics.Where(m => m.MetricType == MetricType.HbA1c).ToList();
        var bloodPressure = metrics
            .Where(m => m.MetricType is MetricType.SystolicBp or MetricType.DiastolicBp)
            .GroupBy(m => m.RecordedAtUtc)
            .Select(g =>
            {
                var systolic = g.OrderByDescending(m => m.Id)
                    .FirstOrDefault(m => m.MetricType == MetricType.SystolicBp);
                var diastolic = g.OrderByDescending(m => m.Id)
                    .FirstOrDefault(m => m.MetricType == MetricType.DiastolicBp);

                return systolic is null || diastolic is null
                    ? null
                    : new BloodPressurePair(
                        systolic,
                        diastolic,
                        systolic.IsAbnormal || diastolic.IsAbnormal);
            })
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        var glucoseAbnormalCount = glucose.Count(m => m.IsAbnormal);
        var hba1cAbnormalCount = hba1c.Count(m => m.IsAbnormal);
        var bloodPressureAbnormalCount = bloodPressure.Count(p => p.IsAbnormal);

        static IReadOnlyList<MetricChartPointDto> BuildGlucoseChart(IEnumerable<HealthMetric> source) =>
            source.GroupBy(m => m.RecordedLocalDate)
                .OrderBy(g => g.Key)
                .Select(g => new MetricChartPointDto
                {
                    Date = g.Key,
                    Value = Math.Round(g.Average(m => m.Value), 1),
                    Count = g.Count(),
                    AbnormalCount = g.Count(m => m.IsAbnormal),
                    IsAbnormal = g.Any(m => m.IsAbnormal)
                })
                .ToList();

        var beforeMealRange = GlucoseThresholds.GetTargetRange(diabetesType, MetricContext.BeforeMeal);
        var afterMealRange = GlucoseThresholds.GetTargetRange(diabetesType, MetricContext.AfterMeal);

        return Ok(new MetricSummaryDto
        {
            Days = days,
            From = from,
            To = today,
            TotalAbnormalCount = glucoseAbnormalCount + hba1cAbnormalCount + bloodPressureAbnormalCount,
            Glucose = new GlucoseTrendDto
            {
                Average = glucose.Count > 0 ? Math.Round(glucose.Average(m => m.Value), 1) : (decimal?)null,
                Latest = ToLatest(glucose.OrderByDescending(m => m.RecordedAtUtc).FirstOrDefault()),
                AbnormalCount = glucoseAbnormalCount,
                // Giữ biểu đồ tổng cho client cũ, đồng thời tách theo bối cảnh để
                // mobile vẽ đúng ngưỡng trước ăn / sau ăn.
                Chart = BuildGlucoseChart(glucose),
                BeforeMealChart = BuildGlucoseChart(
                    glucose.Where(m => m.Context == MetricContext.BeforeMeal)),
                AfterMealChart = BuildGlucoseChart(
                    glucose.Where(m => m.Context == MetricContext.AfterMeal))
            },
            HbA1c = new MetricTrendDto
            {
                Average = hba1c.Count > 0 ? Math.Round(hba1c.Average(m => m.Value), 1) : (decimal?)null,
                Latest = ToLatest(hba1c.OrderByDescending(m => m.RecordedAtUtc).FirstOrDefault()),
                AbnormalCount = hba1cAbnormalCount,
                Chart = hba1c.OrderBy(m => m.RecordedAtUtc)
                    .Select(m => new MetricChartPointDto
                    {
                        Date = m.RecordedLocalDate,
                        RecordedAtUtc = m.RecordedAtUtc,
                        Value = m.Value,
                        Count = 1,
                        AbnormalCount = m.IsAbnormal ? 1 : 0,
                        IsAbnormal = m.IsAbnormal
                    })
                    .ToList()
            },
            BloodPressure = new BloodPressureTrendDto
            {
                AverageSystolic = bloodPressure.Count > 0
                    ? Math.Round(bloodPressure.Average(p => p.Systolic.Value), 1)
                    : (decimal?)null,
                AverageDiastolic = bloodPressure.Count > 0
                    ? Math.Round(bloodPressure.Average(p => p.Diastolic.Value), 1)
                    : (decimal?)null,
                Latest = bloodPressure.OrderByDescending(p => p.Systolic.RecordedAtUtc)
                    .Select(p => new BloodPressureLatestDto
                    {
                        SystolicId = p.Systolic.Id,
                        DiastolicId = p.Diastolic.Id,
                        Systolic = p.Systolic.Value,
                        Diastolic = p.Diastolic.Value,
                        Unit = "mmHg",
                        RecordedAtUtc = p.Systolic.RecordedAtUtc,
                        RecordedLocalDate = p.Systolic.RecordedLocalDate,
                        IsAbnormal = p.IsAbnormal
                    })
                    .FirstOrDefault(),
                AbnormalCount = bloodPressureAbnormalCount,
                Chart = bloodPressure.OrderBy(p => p.Systolic.RecordedAtUtc)
                    .Select(p => new BloodPressureChartPointDto
                    {
                        Date = p.Systolic.RecordedLocalDate,
                        RecordedAtUtc = p.Systolic.RecordedAtUtc,
                        Systolic = p.Systolic.Value,
                        Diastolic = p.Diastolic.Value,
                        IsAbnormal = p.IsAbnormal
                    })
                    .ToList()
            },
            Thresholds = new MetricThresholdsDto
            {
                DiabetesType = diabetesType,
                BeforeMeal = new GlucoseRangeDto
                {
                    Lower = beforeMealRange.Lower,
                    Upper = beforeMealRange.Upper
                },
                AfterMeal = new GlucoseRangeDto
                {
                    Lower = afterMealRange.Lower,
                    Upper = afterMealRange.Upper
                },
                SystolicBpAbnormalFrom = SystolicBpAbnormalFrom,
                DiastolicBpAbnormalFrom = DiastolicBpAbnormalFrom
            }
        });

        static MetricLatestDto? ToLatest(HealthMetric? m) => m is null
            ? null
            : new MetricLatestDto
            {
                Id = m.Id,
                VisitId = m.VisitId,
                MetricType = (byte)m.MetricType,
                Value = m.Value,
                Unit = m.Unit,
                Context = (byte?)m.Context,
                RecordedAtUtc = m.RecordedAtUtc,
                RecordedLocalDate = m.RecordedLocalDate,
                IsAbnormal = m.IsAbnormal
            };
    }
    private sealed record BloodPressurePair(
    HealthMetric Systolic,
    HealthMetric Diastolic,
    bool IsAbnormal);

    /* ---------------------------- LỐI SỐNG ---------------------------- */

    /// <summary>UC-46 — ghi nhật ký ăn uống hoặc vận động.</summary>
    public async Task<ActionResult<List<LifestyleLogDto>>> Lifestyle(
        [FromQuery] int? patientId, [FromQuery] int days = 14)
    {
        var pid = ResolvePatientId(_me, patientId);
        var from = _clock.LocalToday.AddDays(-days);

        var rows = await _repository.GetLifestyleLogsAsync(pid, from);

        var items = rows.Select(MapLifestyle).ToList();
        return Ok(items);
    }
    public async Task<ActionResult<LifestyleLogDto>> CreateLifestyle(CreateLifestyleRequest req)
    {
        var pid = RequireMyPatientId(_me);
        var date = req.LogLocalDate ?? _clock.LocalToday;
        if (date > _clock.LocalToday)
            throw AppException.BadRequest(Msg.InvalidData, "Không thể ghi nhật ký cho ngày tương lai.");

        EnsureLifestyleHasContent(req);
        var log = new LifestyleLog
        {
            PatientId = pid,
            LogLocalDate = date,
            MealNote = req.MealNote?.Trim(),
            MealTags = req.MealTags?.Trim(),
            ExerciseMinutes = req.ExerciseMinutes,
            ExerciseType = req.ExerciseType?.Trim()
        };
        _repository.Add(log);
        await _repository.CommitAsync();
        await _audit.LogAsync(
            AuditAction.LifestyleCreate,
            nameof(LifestyleLog),
            log.Id,
            null,
            new { log.LogLocalDate, log.MealNote, log.MealTags, log.ExerciseMinutes, log.ExerciseType });
        await _repository.CommitAsync();
        return Ok(MapLifestyle(log));
    }

    public async Task<ActionResult<LifestyleLogDto>> UpdateLifestyle(int id, CreateLifestyleRequest req)
    {
        var pid = RequireMyPatientId(_me);
        var log = await _repository.GetLifestyleLogForUpdateAsync(id, pid)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy nhật ký lối sống.");

        _repository.ApplyOriginalRowVersion(log, req.RowVersion);
        var oldLifestyle = new
        {
            log.LogLocalDate,
            log.MealNote,
            log.MealTags,
            log.ExerciseMinutes,
            log.ExerciseType
        };
        EnsureLifestyleHasContent(req);
        var date = req.LogLocalDate ?? log.LogLocalDate;
        if (date > _clock.LocalToday)
            throw AppException.BadRequest(Msg.InvalidData, "Không thể ghi nhật ký cho ngày tương lai.");

        log.LogLocalDate = date;
        log.MealNote = req.MealNote?.Trim();
        log.MealTags = req.MealTags?.Trim();
        log.ExerciseMinutes = req.ExerciseMinutes;
        log.ExerciseType = req.ExerciseType?.Trim();
        await _audit.LogAsync(
            AuditAction.LifestyleUpdate,
            nameof(LifestyleLog),
            log.Id,
            oldLifestyle,
            new { log.LogLocalDate, log.MealNote, log.MealTags, log.ExerciseMinutes, log.ExerciseType });
        await _repository.CommitAsync();
        return Ok(MapLifestyle(log));
    }

    /// <summary>UC-47 — sửa/xoá mềm nhật ký lối sống.</summary>
    public async Task<IActionResult> DeleteLifestyle(int id, ConcurrencyRequest req)
    {
        var l = await _repository.GetLifestyleLogForUpdateAsync(id)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy bản ghi.");
        var myPatientId = RequireMyPatientId(_me);
        if (l.PatientId != myPatientId)
            throw AppException.Forbidden(Msg.Forbidden, "Bạn không có quyền xóa nhật ký này.");
        _repository.ApplyOriginalRowVersion(l, req.RowVersion);

        l.IsDeleted = true;
        l.DeletedAt = _clock.UtcNow;
        await _audit.LogAsync(
            AuditAction.LifestyleDelete,
            nameof(LifestyleLog),
            l.Id,
            new { isDeleted = false, l.LogLocalDate },
            new { isDeleted = true, l.DeletedAt });
        await _repository.CommitAsync();
        return Ok(new { message = "Đã ẩn bản ghi." });
    }

    private static LifestyleLogDto MapLifestyle(LifestyleLog log) => new()
    {
        Id = log.Id,
        LogLocalDate = log.LogLocalDate,
        MealNote = log.MealNote,
        MealTags = log.MealTags,
        ExerciseMinutes = log.ExerciseMinutes,
        ExerciseType = log.ExerciseType,
        RowVersion = log.ToRowVersion()
    };

    /* ---------------------------- THUỐC ---------------------------- */

    /// <summary>UC-44 — lịch uống thuốc hôm nay.</summary>
    public async Task<ActionResult<List<MedicationLogDto>>> Today([FromQuery] int? patientId)
    {
        var pid = ResolvePatientId(_me, patientId);
        var today = _clock.LocalToday;

        var rows = await _repository.GetMedicationLogsForDateAsync(pid, today);
        return Ok(rows.Select(MapMedication).ToList());
    }

    /// <summary>UC-44 — bệnh nhân xác nhận Taken, Skipped hoặc hoàn tác về Pending.</summary>
    public async Task<ActionResult<MedicationLogDto>> UpdateMedicationStatus(
        int id, UpdateMedicationStatusRequest req)
    {
        if (req.Status is not (MedicationStatus.Taken or MedicationStatus.Skipped or MedicationStatus.Pending ))
            throw AppException.BadRequest(Msg.InvalidData, "Trạng thái xác nhận thuốc không hợp lệ.");

        var log = await _repository.GetMedicationLogForUpdateAsync(id)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy lịch uống thuốc.");

        var myPatientId = RequireMyPatientId(_me);
        if (log.PatientId != myPatientId)
            throw AppException.Forbidden(Msg.Forbidden, "Bạn không có quyền cập nhật liều thuốc này.");

        if (log.Status == MedicationStatus.Cancelled)
            throw AppException.BadRequest(Msg.ApptImmutable, "Liều thuốc này đã bị hủy theo đơn đã thu hồi.");
        _repository.ApplyOriginalRowVersion(log, req.RowVersion);
        var oldStatus = log.Status;
        log.Status = req.Status;
        log.TakenAt = req.Status == MedicationStatus.Taken ? _clock.UtcNow : null;
        await _audit.LogAsync(
            AuditAction.MedicationConfirm,
            nameof(MedicationLog),
            log.Id,
            new { status = oldStatus.ToString() },
            new { status = log.Status.ToString(), log.TakenAt });
        await _repository.CommitAsync();
        return Ok(MapMedication(log));
    }

    /* ---------------------------- HỖ TRỢ ---------------------------- */

    /// <summary>
    /// Bệnh nhân luôn bị ép về hồ sơ của chính mình, bất kể tham số truyền lên.
    /// Đây là chốt chặn chống việc đổi patientId trên URL để xem hồ sơ người khác.
    /// </summary>
    private static void EnsureLifestyleHasContent(CreateLifestyleRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.MealNote)
            && string.IsNullOrWhiteSpace(req.MealTags)
            && req.ExerciseMinutes is null
            && string.IsNullOrWhiteSpace(req.ExerciseType))
            throw AppException.BadRequest(Msg.RequiredFields, "Vui lòng nhập thông tin bữa ăn hoặc vận động.");

        if (req.ExerciseMinutes.HasValue &&
            (req.ExerciseMinutes.Value < 0))
        {
            throw AppException.BadRequest(
                Msg.RequiredFields,
                "Số phút vận động phải lớn hơn 0");
        }
       
    }

    private MedicationLogDto MapMedication(MedicationLog m) => new()
    {
        Id = m.Id,
        PrescriptionId = m.PrescriptionItem?.PrescriptionId ?? 0,
        PrescriptionItemId = m.PrescriptionItemId,
        DrugName = m.PrescriptionItem?.DrugName ?? "",
        Dose = m.PrescriptionItem?.Dose ?? "",
        ScheduledAt = _clock.ToLocal(m.ScheduledAt)!.Value,
        TakenAt = _clock.ToLocal(m.TakenAt),
        Status = (byte)m.Status,
        StatusLabel = MedicationStatusLabel(m.Status),
        RowVersion = m.ToRowVersion()
    };

    private static string MedicationStatusLabel(MedicationStatus status) => status switch
    {
        MedicationStatus.Pending => "Chưa xác nhận",
        MedicationStatus.Taken => "Đã uống",
        MedicationStatus.Skipped => "Bỏ liều",
        MedicationStatus.Missed => "Quá hạn",
        MedicationStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    private static HealthMetricDto ToMetricDto(HealthMetric metric) => new()
    {
        Id = metric.Id,
        VisitId = metric.VisitId,
        MetricType = (byte)metric.MetricType,
        Value = metric.Value,
        Unit = metric.Unit,
        Context = (byte?)metric.Context,
        RecordedAtUtc = metric.RecordedAtUtc,
        RecordedLocalDate = metric.RecordedLocalDate,
        Note = metric.Note,
        IsAbnormal = metric.IsAbnormal,
        RowVersion = metric.ToRowVersion()
    };


    private async Task<byte> GetPatientDiabetesTypeAsync(int patientId)
    {
        var patient = await _repository.GetPatientByIdAsync(patientId, tracking: false)
            ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");

        return patient.DiabetesType;
    }
    private async Task<bool> IsAbnormalAsync(MetricType type, decimal value, MetricContext? ctx, byte diabetesType)
    {
        switch (type)
        {
            case MetricType.Glucose:
                return GlucoseThresholds.IsAbnormal(diabetesType, value, ctx);

            case MetricType.HbA1c:
                return value > await _cfg.GetDecimalAsync("metric.hba1c_target", 7.0m);

            case MetricType.SystolicBp:
                return value >= SystolicBpAbnormalFrom;

            case MetricType.DiastolicBp:
                return value >= DiastolicBpAbnormalFrom;

            default:
                return false;
        }

    }

}
