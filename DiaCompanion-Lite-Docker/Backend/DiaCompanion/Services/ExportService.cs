using System.Text;
using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

/// <summary>UC-34, UC-35 — kết xuất dữ liệu.</summary>
public class ExportService : BaseService, IExportService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IClinicClock _clock;

    public ExportService(
        IRepository repository,
        ICurrentUser me,
        IAuditService audit,
        IClinicClock clock)
    {
        _repository = repository;
        _me = me;
        _audit = audit;
        _clock = clock;
    }

    /// <summary>
    /// UC-34 — dữ liệu có cấu trúc của báo cáo khám. Endpoint PDF riêng bên dưới
    /// sinh tệp chính thức; endpoint này phục vụ màn xem trước hoặc client cần tự in.
    /// Mọi actor chỉ được xuất sau khi lượt khám đã hoàn tất.
    /// </summary>
    public async Task<IActionResult> VisitReport(int visitId)
    {
        var visit = await _repository.GetVisitForExportAsync(visitId)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy lượt khám.");

        EnsureCanReadPatient(
    _me,
    visit.MedicalRecord.PatientId);

        // Báo cáo khám chỉ được phát hành sau khi bác sĩ phụ trách đóng lượt khám.
        if (visit.Status != VisitStatus.Completed)
            throw AppException.Forbidden(Msg.Forbidden,
                "Lượt khám chưa hoàn tất nên chưa thể xuất báo cáo chính thức.");

        var reviewRows = await _repository.GetVisitDiagnosisReviewsForExportAsync(visitId);
        var findings = reviewRows.Select(r => new
        {
            DiagnosisId = r.AiDiagnosis!.Id,
            // Id của bản ghi AI. Frontend cần nó để gọi
            // /api/diagnoses/{id}/lesion-mask và /fractal-image.
            // Thiếu trường này thì ReportAiImage không có gì để gọi và luôn
            // hiện "Chưa có ảnh", dù mask vẫn nằm nguyên trên đĩa — đúng lý do
            // trang xem kết quả AI hiển thị được còn trang báo cáo thì không.
            
            Eye = (byte)r.AiDiagnosis!.FundusImage!.Eye,
            ImageId = r.AiDiagnosis.FundusImageId,
            FinalGrade = (byte)r.FinalGrade,
            Action = (byte)r.Action,
            r.Reason,
            AiGrade = (byte)r.AiDiagnosis.DrGrade,
            r.AiDiagnosis.Disagreement,
            r.AiDiagnosis.IsDeferred,
            r.AiDiagnosis.FractalDimension,
            r.AiDiagnosis.CountMA,
            r.AiDiagnosis.CountHE,
            r.AiDiagnosis.CountEX,
            r.AiDiagnosis.CountSE,
            ModelVersion = ModelSetLabel(r.AiDiagnosis),
            DoctorName = r.Doctor!.FullName,
            r.CreatedAt,
            urlImageLesionAfterMedical = r.AiDiagnosis.LesionMaskPath,
            urlImageVesselAfterMedical = r.AiDiagnosis.VesselMaskPath,
            urlImgBeforeMEDICAL = r.AiDiagnosis.FundusImage.FilePath
        }).ToList();

        var metricRows = await _repository.GetVisitHealthMetricsForExportAsync(visitId);
        var glucoseMetric = metricRows.FirstOrDefault(m => m.MetricType == MetricType.Glucose);
        var hba1cMetric = metricRows.FirstOrDefault(m => m.MetricType == MetricType.HbA1c);
        var systolicMetric = metricRows.FirstOrDefault(m => m.MetricType == MetricType.SystolicBp);
        var diastolicMetric = metricRows.FirstOrDefault(m => m.MetricType == MetricType.DiastolicBp);

        var prescriptionRows = await _repository.GetVisitPrescriptionsForExportAsync(visitId);
        var prescriptions = prescriptionRows.Select(p => new
        {
            p.IssuedAt,
            p.Note,
            Items = p.Items.Where(i => i.IsActive)
                .Select(i => new { i.DrugName, i.Dose, i.TimesPerDay, i.DurationDays, i.Instruction })
                .ToList()
        }).ToList();

        await _audit.LogAsync(AuditAction.Export, nameof(Visit), visitId, detail: "Xuất báo cáo khám");
        await _repository.CommitAsync();

        return Ok(new
        {
            clinic = new { name = "DiaCompanion", subtitle = "Hệ thống hỗ trợ sàng lọc bệnh võng mạc đái tháo đường" },
            patient = new
            {
                visit.MedicalRecord.Patient!.Code,
                visit.MedicalRecord.Patient.FullName,
                visit.MedicalRecord.Patient.DateOfBirth,
                visit.MedicalRecord.Patient.Gender,
                visit.MedicalRecord.Patient.Phone,
                visit.MedicalRecord.Patient.DiabetesType,
                visit.MedicalRecord.Patient.DiabetesDurationYears
            },
            visit = new
            {
                visit.Id,
                VisitDate = _clock.ToLocal(visit.VisitDate)!.Value,
                Status = (byte)visit.Status,
                visit.Conclusion,
                Referral = (byte?)visit.Referral,
                visit.RecheckMonths,
                ClosedAt = _clock.ToLocal(visit.ClosedAt),
                DoctorName = visit.Doctor?.FullName,
                DoctorLicense = visit.Doctor?.LicenseNo
            },
            findings = findings.Select(f => new
            {
                f.DiagnosisId,
                //urlImageLesionAfterMedical = r.AiDiagnosis.LesionMaskPath,
                //urlImageVesselAfterMedical = r.AiDiagnosis.VesselMaskPath,
                //urlImgBeforeMEDICAL = r.AiDiagnosis.FundusImage.FilePath
                f.urlImageLesionAfterMedical,
                f.urlImageVesselAfterMedical,
                f.urlImgBeforeMEDICAL,
                f.Eye,
                f.ImageId,
                finalGrade = f.FinalGrade,
                finalGradeLabel = DiagnosesService.GradeLabel(f.FinalGrade),
                confirmedBy = f.DoctorName,
                CreatedAt = _clock.ToLocal(f.CreatedAt)!.Value,
                // Ghi rõ AI đề xuất gì và bác sĩ quyết định gì — minh bạch cho hồ sơ
                ai = new
                {
                    grade = f.AiGrade,
                    gradeLabel = DiagnosesService.GradeLabel(f.AiGrade),
                    f.Disagreement,
                    f.IsDeferred,
                    model = f.ModelVersion,
                    wasOverridden = f.Action == 1,
                    overrideReason = f.Reason
                },
                lesions = new { f.CountMA, f.CountHE, f.CountEX, f.CountSE },
                fractal = f.FractalDimension
            }),
            healthMetrics = new
            {
                glucose = glucoseMetric is null ? null : new
                {
                    glucoseMetric.Value,
                    glucoseMetric.Unit,
                    Context = (byte?)glucoseMetric.Context,
                    glucoseMetric.IsAbnormal,
                    glucoseMetric.RecordedAtUtc,
                    glucoseMetric.Note
                },
                hba1c = hba1cMetric is null ? null : new
                {
                    hba1cMetric.Value,
                    hba1cMetric.Unit,
                    hba1cMetric.IsAbnormal,
                    hba1cMetric.RecordedAtUtc,
                    hba1cMetric.Note
                },
                bloodPressure = systolicMetric is null && diastolicMetric is null ? null : new
                {
                    systolic = systolicMetric?.Value,
                    diastolic = diastolicMetric?.Value,
                    unit = "mmHg",
                    isAbnormal = systolicMetric?.IsAbnormal == true || diastolicMetric?.IsAbnormal == true,
                    recordedAtUtc = systolicMetric?.RecordedAtUtc ?? diastolicMetric?.RecordedAtUtc,
                    note = systolicMetric?.Note ?? diastolicMetric?.Note
                }
            },
            prescriptions,
            disclaimer = "Kết quả AI mang tính hỗ trợ quyết định. Phân độ cuối cùng do bác sĩ xác lập.",
            generatedAt = _clock.LocalNow
        });
    }

    /// <summary>UC-34 — sinh tệp PDF báo cáo khám đã được bác sĩ xác nhận.</summary>
    public async Task<IActionResult> VisitReportPdf(int visitId)
    {
        var visit = await _repository.GetVisitForExportAsync(visitId)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy lượt khám.");

        EnsureCanReadPatient(
    _me,
    visit.MedicalRecord.PatientId);
        if (visit.Status != VisitStatus.Completed)
            throw AppException.Forbidden(Msg.Forbidden,
                "Lượt khám chưa hoàn tất nên chưa thể xuất báo cáo PDF.");

        var reviewRows = await _repository.GetVisitDiagnosisReviewsForExportAsync(visitId);
        var findings = reviewRows.Select(r => new
        {
            Eye = (byte)r.AiDiagnosis!.FundusImage!.Eye,
            AiGrade = (byte)r.AiDiagnosis.DrGrade,
            FinalGrade = (byte)r.FinalGrade,
            r.Action,
            r.Reason,
            Model = ModelSetLabel(r.AiDiagnosis),
            ConfirmedBy = r.Doctor!.FullName
        }).ToList();

        var metricRows = await _repository.GetVisitHealthMetricsForExportAsync(visitId);
        var glucoseMetric = metricRows.FirstOrDefault(m => m.MetricType == MetricType.Glucose);
        var hba1cMetric = metricRows.FirstOrDefault(m => m.MetricType == MetricType.HbA1c);
        var systolicMetric = metricRows.FirstOrDefault(m => m.MetricType == MetricType.SystolicBp);
        var diastolicMetric = metricRows.FirstOrDefault(m => m.MetricType == MetricType.DiastolicBp);

        var prescriptionRows = await _repository.GetVisitPrescriptionsForExportAsync(visitId);
        var prescriptions = prescriptionRows.SelectMany(p => p.Items.Where(i => i.IsActive)
            .Select(i => new
            {
                i.DrugName,
                i.Dose,
                i.TimesPerDay,
                i.DurationDays,
                i.Instruction
            })).ToList();

        var visitLocal = _clock.ToLocal(visit.VisitDate) ?? visit.VisitDate;

        var lines = new List<string>
        {
            "DIACOMPANION - EXAMINATION REPORT",
            "Diabetic Retinopathy Screening Support System",
            "",
            $"Report ID: VISIT-{visit.Id}",
            $"Patient code: {visit.MedicalRecord.Patient!.Code}",
            $"Patient name: {visit.MedicalRecord.Patient.FullName}",
            $"Date of birth: {visit.MedicalRecord.Patient.DateOfBirth:dd/MM/yyyy}",
            $"Phone: {visit.MedicalRecord.Patient.Phone}",
            $"Visit date: {visitLocal:dd/MM/yyyy HH:mm}",
            $"Assigned doctor: {visit.Doctor?.FullName ?? "N/A"}",
            $"License number: {visit.Doctor?.LicenseNo ?? "N/A"}",
            "",
            "CLINICAL CONCLUSION",
            visit.Conclusion ?? "No conclusion recorded.",
            $"Referral: {visit.Referral?.ToString() ?? "None"}",
            $"Re-screen interval: {visit.RecheckMonths?.ToString() ?? "N/A"} month(s)",
            "",
            "DOCTOR-CONFIRMED RETINAL FINDINGS"
        };

        if (findings.Count == 0)
        {
            lines.Add("No confirmed retinal finding.");
        }
        else
        {
            foreach (var finding in findings)
            {
                var eye = finding.Eye == (byte)Eye.Od ? "OD" : finding.Eye == (byte)Eye.Os ? "OS" : finding.Eye.ToString();
                lines.Add(
                    $"{eye}: final {DiagnosesService.GradeLabel(finding.FinalGrade)}; " +
                    $"AI {DiagnosesService.GradeLabel(finding.AiGrade)}; " +
                    $"model {finding.Model}; confirmed by {finding.ConfirmedBy}.");
                if (finding.Action == ReviewAction.Override && !string.IsNullOrWhiteSpace(finding.Reason))
                    lines.Add($"  Override reason: {finding.Reason}");
            }
        }

        lines.Add("");
        lines.Add("HEALTH METRICS");
        lines.Add(glucoseMetric is null
            ? "Glucose: N/A"
            : $"Glucose: {glucoseMetric.Value:0.##} {glucoseMetric.Unit} ({glucoseMetric.Context?.ToString() ?? "N/A"})");
        lines.Add(hba1cMetric is null
            ? "HbA1c: N/A"
            : $"HbA1c: {hba1cMetric.Value:0.##} {hba1cMetric.Unit}");
        lines.Add(systolicMetric is null || diastolicMetric is null
            ? "Blood pressure: N/A"
            : $"Blood pressure: {systolicMetric.Value:0}/{diastolicMetric.Value:0} mmHg");

        lines.Add("");
        lines.Add("PRESCRIPTION");
        if (prescriptions.Count == 0)
        {
            lines.Add("No prescription recorded for this visit.");
        }
        else
        {
            foreach (var item in prescriptions)
            {
                lines.Add(
                    $"- {item.DrugName}, {item.Dose}, {item.TimesPerDay} time(s)/day, " +
                    $"{item.DurationDays} day(s). {item.Instruction}");
            }
        }

        lines.Add("");
        lines.Add("AI results support clinical decision-making only. The final grade is confirmed by a doctor.");
        lines.Add($"Generated at: {_clock.LocalNow:dd/MM/yyyy HH:mm}");

        var pdf = SimplePdfDocument.Create(lines);

        await _audit.LogAsync(AuditAction.Export, nameof(Visit), visitId,
            detail: "Xuất báo cáo khám PDF");
        await _repository.CommitAsync();

        return File(pdf, "application/pdf", $"examination-report-{visitId}.pdf");
    }

    /// <summary>
    /// UC-35 — tập ca người–máy mâu thuẫn.
    ///
    /// Đây là dữ liệu đánh giá chính của đề tài: nếu tỉ lệ ghi đè trong nhóm
    /// BỊ GẮN CỜ cao hơn hẳn nhóm không gắn cờ, nghĩa là cơ chế deferral đang
    /// bắt đúng những ca mà mô hình thực sự sai.
    /// </summary>
    public async Task<ActionResult<object>> DisagreementCases(int? modelVersionId, DateOnly? from, DateOnly? to)
    {
        var (fromUtc, toExclusiveUtc) = ResolveReviewDateRange(from, to);
        var reviewRows = await _repository.GetDiagnosisReviewsForExportAsync(
            modelVersionId, fromUtc, toExclusiveUtc, overridesOnly: true);

        var rawCases = reviewRows.Select(r => new
        {
            r.AiDiagnosisId,
            PatientCode = r.AiDiagnosis!.FundusImage!.Patient!.Code,
            Eye = r.AiDiagnosis.FundusImage.Eye,
            ModelVersion = ModelSetLabel(r.AiDiagnosis),
            AiGrade = r.AiDiagnosis.DrGrade,
            DoctorGrade = r.FinalGrade,
            Disagreement = r.AiDiagnosis.Disagreement,
            WasDeferred = r.AiDiagnosis.IsDeferred,
            r.Reason,
            ReviewedAt = r.CreatedAt
        }).ToList();

    var cases = rawCases
        .Select(r => new DisagreementCaseDto
        {
            AiDiagnosisId = r.AiDiagnosisId,
            PatientCode = r.PatientCode,

            // Chuyển enum sang byte sau khi dữ liệu đã được đọc khỏi SQL.
            Eye = (byte)r.Eye,
            ModelVersion = r.ModelVersion,
            AiGrade = (byte)r.AiGrade,
            DoctorGrade = (byte)r.DoctorGrade,

            GradeDistance = Math.Abs(
                (int)r.DoctorGrade -
                (int)r.AiGrade
            ),

            Disagreement = r.Disagreement,
            WasDeferred = r.WasDeferred,
            Reason = r.Reason,
            ReviewedAt = _clock.ToLocal(r.ReviewedAt)!.Value
        })
        .OrderByDescending(r => r.GradeDistance)
        .ThenByDescending(r => r.ReviewedAt)
        .ToList();

        // Tính chỉ số tổng hợp trên TOÀN BỘ review, không chỉ ca ghi đè.
        var allReviewRows = await _repository.GetDiagnosisReviewsForExportAsync(
            modelVersionId, fromUtc, toExclusiveUtc, overridesOnly: false);
        var allReviews = allReviewRows
            .Select(r => new { Action = (byte)r.Action, Deferred = r.AiDiagnosis!.IsDeferred })
            .ToList();

        var deferredSet = allReviews.Where(r => r.Deferred).ToList();
        var notDeferredSet = allReviews.Where(r => !r.Deferred).ToList();

        var rateIn = deferredSet.Count == 0 ? 0
            : Math.Round(deferredSet.Count(r => r.Action == 1) * 100m / deferredSet.Count, 1);
        var rateOut = notDeferredSet.Count == 0 ? 0
            : Math.Round(notDeferredSet.Count(r => r.Action == 1) * 100m / notDeferredSet.Count, 1);

        var summary = new DisagreementSummaryDto
        {
            TotalReviewed = allReviews.Count,
            TotalOverridden = allReviews.Count(r => r.Action == 1),
            OverrideRate = allReviews.Count == 0 ? 0
                : Math.Round(allReviews.Count(r => r.Action == 1) * 100m / allReviews.Count, 1),
            DeferredCount = deferredSet.Count,
            OverrideRateWithinDeferred = rateIn,
            OverrideRateOutsideDeferred = rateOut,
            AvgDisagreement = cases.Count == 0 ? 0
                : Math.Round(cases.Average(c => c.Disagreement ?? 0), 4),
            Interpretation = rateIn > rateOut
                ? $"Tỉ lệ ghi đè trong nhóm gắn cờ ({rateIn}%) cao hơn nhóm không gắn cờ ({rateOut}%) — " +
                  "cơ chế chuyển bác sĩ đang tập trung đúng vào các ca mô hình dễ sai."
                : $"Tỉ lệ ghi đè trong nhóm gắn cờ ({rateIn}%) chưa cao hơn nhóm không gắn cờ ({rateOut}%) — " +
                  "cần xem lại ngưỡng hoặc cách tính bất đồng."
        };

        await _audit.LogAsync(AuditAction.Export, "DisagreementCases", null,
            detail: $"Kết xuất {cases.Count} ca mâu thuẫn");
        await _repository.CommitAsync();

        return Ok(new { summary, cases });
    }

    /// <summary>UC-35 — kết xuất CSV để phân tích ngoài hệ thống.</summary>
    public async Task<IActionResult> DisagreementCsv(int? modelVersionId, DateOnly? from, DateOnly? to)
    {
        var (fromUtc, toExclusiveUtc) = ResolveReviewDateRange(from, to);
        var reviewRows = await _repository.GetDiagnosisReviewsForExportAsync(
            modelVersionId, fromUtc, toExclusiveUtc, overridesOnly: true);
        var rows = reviewRows.Select(r => new
        {
            r.AiDiagnosisId,
            PatientCode = r.AiDiagnosis!.FundusImage!.Patient!.Code,
            Eye = (byte)r.AiDiagnosis.FundusImage.Eye,
            Model = ModelSetLabel(r.AiDiagnosis),
            AiGrade = (byte)r.AiDiagnosis.DrGrade,
            LesionGrade = (byte?)r.AiDiagnosis.LesionGradeImplied,
            DoctorGrade = (byte)r.FinalGrade,
            r.AiDiagnosis.Disagreement,
            r.AiDiagnosis.IsDeferred,
            r.AiDiagnosis.FractalDimension,
            r.Reason,
            r.CreatedAt
        }).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("ai_diagnosis_id,patient_code,eye,model,ai_grade,lesion_implied_grade,doctor_grade," +
                      "grade_distance,disagreement,was_deferred,fractal_dimension,reviewed_at,reason");

        foreach (var r in rows)
        {
            var distance = Math.Abs(r.DoctorGrade - r.AiGrade);
            // Bọc lý do trong dấu nháy kép và nhân đôi nháy bên trong, vì lý do
            // là văn bản tự do có thể chứa dấu phẩy
            var reason = (r.Reason ?? "").Replace("\"", "\"\"");
            var reviewedLocal = _clock.ToLocal(r.CreatedAt) ?? r.CreatedAt;
            sb.AppendLine($"{r.AiDiagnosisId},{r.PatientCode},{r.Eye},{r.Model},{r.AiGrade}," +
                          $"{r.LesionGrade},{r.DoctorGrade},{distance},{r.Disagreement}," +
                          $"{(r.IsDeferred ? 1 : 0)},{r.FractalDimension},{reviewedLocal:O},\"{reason}\"");
        }

        await _audit.LogAsync(AuditAction.Export, "DisagreementCases", null,
            detail: $"Kết xuất CSV {rows.Count} ca");
        await _repository.CommitAsync();

        // BOM để Excel mở đúng tiếng Việt
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"disagreement-cases-{_clock.LocalNow:yyyyMMdd}.csv");
    }

    /// <summary>
    /// Bộ dữ liệu nghiên cứu cho Gap 2 (đánh giá cơ chế deferral).
    /// MỖI DÒNG = một lần chạy AI của một mắt, kể cả chưa được bác sĩ xác nhận.
    /// ĐÃ BỎ mọi trường định danh (họ tên, SĐT, ngày sinh, địa chỉ); chỉ giữ mã
    /// bệnh nhân giả lập và tuổi tại thời điểm khám. Các biến bám sát công thức
    /// deferral: bất đồng chéo, điểm nguy cơ nền (HbA1c, thời gian mắc, HA tâm thu
    /// cho T2, tuân thủ, quá hạn tái khám), ngưỡng hiệu dụng, cùng phân độ tham
    /// chiếu của bác sĩ để tính risk–coverage / baseline / ablation ngoài hệ thống.
    /// </summary>
    public async Task<IActionResult> ResearchDatasetCsv(DateOnly? from, DateOnly? to)
    {
        var (fromUtc, toExclusiveUtc) = ResolveReviewDateRange(from, to);
        var rows = await _repository.GetResearchDatasetAsync(fromUtc, toExclusiveUtc);

        // Chỉ số theo lượt khám: nạp một lần cho tất cả lượt liên quan rồi tra cứu.
        var visitIds = rows
            .Select(d => d.FundusImage?.VisitId)
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var metrics = await _repository.GetHealthMetricsForVisitsAsync(visitIds);
        // (VisitId, MetricType) -> giá trị mới nhất theo Id.
        var latestByVisitType = metrics
            .GroupBy(m => (m.VisitId!.Value, (byte)m.MetricType))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.Id).First().Value);

        decimal? Metric(int? visitId, byte type) =>
            visitId is int v && latestByVisitType.TryGetValue((v, type), out var val) ? val : (decimal?)null;

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",",
            "ai_diagnosis_id", "patient_code", "gender", "age_at_visit", "diabetes_type",
            "diabetes_duration_years", "baseline_hba1c",
            "visit_id", "visit_date", "visit_status", "recheck_months",
            "visit_hba1c", "visit_glucose_mmol_l", "visit_systolic_bp", "visit_diastolic_bp",
            "image_id", "eye", "quality_status",
            "ai_run_at", "ai_grade", "lesion_implied_grade",
            "count_ma", "count_he", "count_ex", "count_se",
            "area_ma", "area_he", "area_ex", "area_se",
            "disagreement", "clinical_risk_score", "clinical_risk_factors",
            "effective_disagreement_threshold", "base_disagreement_threshold",
            "is_deferred", "defer_reason",
            "fractal_dimension", "fractal_st", "fractal_sn", "fractal_it", "fractal_in",
            "fractal_asymmetry", "fractal_tn", "lacunarity",
            "dr_model_version", "lesion_model_version", "fractal_model_version",
            "is_reviewed", "review_action", "doctor_grade", "grade_distance",
            "override_reason", "reviewed_at", "reviewed_by_doctor"));

        foreach (var d in rows)
        {
            var img = d.FundusImage;
            var visit = img?.Visit;
            var patient = visit?.MedicalRecord?.Patient;
            var visitId = img?.VisitId;

            // Review còn hiệu lực (query filter đã loại review void). Tối đa một.
            var review = d.Reviews?.OrderByDescending(r => r.CreatedAt).FirstOrDefault();
            int? ageAtVisit = patient != null && visit != null
                ? YearsBetween(patient.DateOfBirth, visit.VisitDate)
                : null;
            int? gradeDistance = review != null ? Math.Abs((int)review.FinalGrade - (int)d.DrGrade) : null;

            var cells = new object?[]
            {
                d.Id,
                patient?.Code,
                patient == null ? null : (byte?)patient.Gender,
                ageAtVisit,
                patient == null ? null : (byte?)patient.DiabetesType,
                patient?.DiabetesDurationYears,
                patient?.BaselineHbA1c,
                visitId,
                visit == null ? null : _clock.ToLocal(visit.VisitDate)?.ToString("O"),
                visit == null ? null : (byte?)visit.Status,
                visit?.RecheckMonths,
                Metric(visitId, 2),  // HbA1c
                Metric(visitId, 1),  // Glucose
                Metric(visitId, 3),  // Systolic
                Metric(visitId, 4),  // Diastolic
                img?.Id,
                img == null ? null : (img.Eye == Eye.Od ? "OD" : "OS"),
                img == null ? null : (byte?)img.QualityStatus,
                _clock.ToLocal(d.CreatedAt)?.ToString("O"),
                (byte)d.DrGrade,
                d.LesionGradeImplied == null ? null : (byte?)d.LesionGradeImplied,
                d.CountMA, d.CountHE, d.CountEX, d.CountSE,
                d.AreaMA, d.AreaHE, d.AreaEX, d.AreaSE,
                d.Disagreement,
                d.ClinicalRiskScore,
                d.ClinicalRiskFactors,
                d.EffectiveDisagreementThreshold,
                d.DisagreementThreshold,
                d.IsDeferred ? 1 : 0,
                d.DeferReason == null ? null : (byte?)d.DeferReason,
                d.FractalDimension, d.FractalSt, d.FractalSn, d.FractalIt, d.FractalIn,
                d.FractalAsymmetry, d.FractalTn, d.Lacunarity,
                d.ModelVersion?.Name,
                d.LesionModelVersion?.Name,
                d.FractalModelVersion?.Name,
                review != null ? 1 : 0,
                review == null ? null : (byte?)review.Action,
                review == null ? null : (byte?)review.FinalGrade,
                gradeDistance,
                review?.Reason,
                review == null ? null : _clock.ToLocal(review.CreatedAt)?.ToString("O"),
                review?.Doctor?.FullName,
            };
            sb.AppendLine(string.Join(",", cells.Select(Csv)));
        }

        await _audit.LogAsync(AuditAction.Export, "ResearchDataset", null,
            detail: $"Kết xuất dataset nghiên cứu {rows.Count} dòng (Gap 2)");
        await _repository.CommitAsync();

        // BOM để Excel mở đúng tiếng Việt.
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"diacompanion-dataset-{_clock.LocalNow:yyyyMMdd}.csv");
    }

    /// <summary>Số năm tròn giữa ngày sinh và ngày khám (không âm).</summary>
    private static int YearsBetween(DateOnly dob, DateTime visitDate)
    {
        var v = DateOnly.FromDateTime(visitDate);
        var years = v.Year - dob.Year;
        if (v < dob.AddYears(years)) years--;
        return years < 0 ? 0 : years;
    }

    /// <summary>Một ô CSV an toàn: bọc nháy khi có dấu phẩy/nháy/xuống dòng; số/thời gian giữ nguyên.</summary>
    private static string Csv(object? value)
    {
        if (value == null) return "";
        var s = value switch
        {
            decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            double db => db.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            s = "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static string ModelSetLabel(AiDiagnosis diagnosis)
    {
        var dr = diagnosis.ModelVersion?.Name ?? "legacy/unknown";
        var lesion = diagnosis.LesionModelVersion?.Name ?? dr;
        var fractal = diagnosis.FractalModelVersion?.Name ?? dr;
        return $"DR={dr} | Lesion={lesion} | Fractal={fractal}";
    }

    private (DateTime? FromUtc, DateTime? ToExclusiveUtc) ResolveReviewDateRange(DateOnly? from, DateOnly? to)
    {
        DateTime? fromUtc = from is DateOnly fromDate
            ? _clock.ToUtc(fromDate.ToDateTime(TimeOnly.MinValue))
            : null;
        DateTime? toExclusiveUtc = to is DateOnly toDate
            ? _clock.ToUtc(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue))
            : null;
        return (fromUtc, toExclusiveUtc);
    }

}
