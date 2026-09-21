/* Bộ dữ liệu nghiên cứu Gap 2 — CÙNG cột, cùng thứ tự với endpoint web
   GET /api/research/dataset.csv (vai trò Research). MỖI DÒNG = một lần chạy AI của
   một mắt (kể cả chưa xác nhận), đã loại bản ghi void.
   ĐÃ BỎ mọi trường định danh (họ tên, SĐT, ngày sinh, địa chỉ).
   Thời gian đổi sang giờ Việt Nam. Ô văn bản được bọc nháy kép để an toàn với dấu phẩy.
   Chạy: docker compose run --rm dbinit export   → ./exports/dataset.csv
   CHƯA chạy thử trên SQL Server — đối chiếu vài dòng với bản tải từ web trước khi dùng. */
SET NOCOUNT ON;

SELECT
    CAST(d.Id AS varchar(20))                                                  AS ai_diagnosis_id,
    p.Code                                                                     AS patient_code,
    CAST(p.Gender AS varchar(3))                                               AS gender,
    CAST(DATEDIFF(YEAR, p.DateOfBirth, v.VisitDate)
         - CASE WHEN DATEADD(YEAR, DATEDIFF(YEAR, p.DateOfBirth, v.VisitDate), p.DateOfBirth) > CAST(v.VisitDate AS date) THEN 1 ELSE 0 END
         AS varchar(4))                                                        AS age_at_visit,
    CAST(p.DiabetesType AS varchar(3))                                         AS diabetes_type,
    CAST(p.DiabetesDurationYears AS varchar(6))                                AS diabetes_duration_years,
    CAST(p.BaselineHbA1c AS varchar(12))                                       AS baseline_hba1c,
    CAST(v.Id AS varchar(20))                                                  AS visit_id,
    CONVERT(varchar(33), v.VisitDate AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time', 126) AS visit_date,
    CAST(v.Status AS varchar(3))                                               AS visit_status,
    CAST(v.RecheckMonths AS varchar(4))                                        AS recheck_months,
    CAST((SELECT TOP 1 m.Value FROM dbo.HealthMetrics m WHERE m.VisitId = v.Id AND m.MetricType = 2 AND m.IsDeleted = 0 ORDER BY m.Id DESC) AS varchar(20)) AS visit_hba1c,
    CAST((SELECT TOP 1 m.Value FROM dbo.HealthMetrics m WHERE m.VisitId = v.Id AND m.MetricType = 1 AND m.IsDeleted = 0 ORDER BY m.Id DESC) AS varchar(20)) AS visit_glucose_mmol_l,
    CAST((SELECT TOP 1 m.Value FROM dbo.HealthMetrics m WHERE m.VisitId = v.Id AND m.MetricType = 3 AND m.IsDeleted = 0 ORDER BY m.Id DESC) AS varchar(20)) AS visit_systolic_bp,
    CAST((SELECT TOP 1 m.Value FROM dbo.HealthMetrics m WHERE m.VisitId = v.Id AND m.MetricType = 4 AND m.IsDeleted = 0 ORDER BY m.Id DESC) AS varchar(20)) AS visit_diastolic_bp,
    CAST(i.Id AS varchar(20))                                                  AS image_id,
    CASE i.Eye WHEN 0 THEN 'OD' ELSE 'OS' END                                  AS eye,
    CAST(i.QualityStatus AS varchar(3))                                        AS quality_status,
    CONVERT(varchar(33), d.CreatedAt AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time', 126) AS ai_run_at,
    CAST(d.DrGrade AS varchar(3))                                              AS ai_grade,
    CAST(d.LesionGradeImplied AS varchar(3))                                   AS lesion_implied_grade,
    CAST(d.CountMA AS varchar(12)) AS count_ma, CAST(d.CountHE AS varchar(12)) AS count_he,
    CAST(d.CountEX AS varchar(12)) AS count_ex, CAST(d.CountSE AS varchar(12)) AS count_se,
    CAST(d.AreaMA AS varchar(24)) AS area_ma, CAST(d.AreaHE AS varchar(24)) AS area_he,
    CAST(d.AreaEX AS varchar(24)) AS area_ex, CAST(d.AreaSE AS varchar(24)) AS area_se,
    CAST(d.Disagreement AS varchar(12))                                        AS disagreement,
    CAST(d.ClinicalRiskScore AS varchar(4))                                    AS clinical_risk_score,
    '"' + REPLACE(ISNULL(d.ClinicalRiskFactors, ''), '"', '""') + '"'         AS clinical_risk_factors,
    CAST(d.EffectiveDisagreementThreshold AS varchar(12))                      AS effective_disagreement_threshold,
    CAST(d.DisagreementThreshold AS varchar(12))                               AS base_disagreement_threshold,
    CAST(d.IsDeferred AS varchar(2))                                           AS is_deferred,
    CAST(d.DeferReason AS varchar(3))                                          AS defer_reason,
    CAST(d.FractalDimension AS varchar(24)) AS fractal_dimension,
    CAST(d.FractalSt AS varchar(24)) AS fractal_st, CAST(d.FractalSn AS varchar(24)) AS fractal_sn,
    CAST(d.FractalIt AS varchar(24)) AS fractal_it, CAST(d.FractalIn AS varchar(24)) AS fractal_in,
    CAST(d.FractalAsymmetry AS varchar(24)) AS fractal_asymmetry,
    CAST(d.FractalTn AS varchar(24)) AS fractal_tn, CAST(d.Lacunarity AS varchar(24)) AS lacunarity,
    mvDr.Name AS dr_model_version, mvLe.Name AS lesion_model_version, mvFr.Name AS fractal_model_version,
    CASE WHEN r.Id IS NULL THEN '0' ELSE '1' END                               AS is_reviewed,
    CAST(r.Action AS varchar(3))                                               AS review_action,
    CAST(r.FinalGrade AS varchar(3))                                           AS doctor_grade,
    CAST(ABS(CAST(r.FinalGrade AS int) - CAST(d.DrGrade AS int)) AS varchar(3)) AS grade_distance,
    '"' + REPLACE(ISNULL(r.Reason, ''), '"', '""') + '"'                       AS override_reason,
    CONVERT(varchar(33), r.CreatedAt AT TIME ZONE 'UTC' AT TIME ZONE 'SE Asia Standard Time', 126) AS reviewed_at,
    '"' + REPLACE(ISNULL(u.FullName, ''), '"', '""') + '"'                     AS reviewed_by_doctor
FROM dbo.AiDiagnoses d
JOIN dbo.FundusImages   i  ON i.Id = d.FundusImageId
JOIN dbo.MedicalVisits  v  ON v.Id = i.VisitId
JOIN dbo.MedicalRecords mr ON mr.Id = v.MedicalRecordId
JOIN dbo.Patients       p  ON p.Id = mr.PatientId
LEFT JOIN dbo.DiagnosisReviews r ON r.AiDiagnosisId = d.Id AND r.IsVoided = 0
LEFT JOIN dbo.Users u ON u.Id = r.DoctorId
LEFT JOIN dbo.ModelVersions mvDr ON mvDr.Id = d.ModelVersionId
LEFT JOIN dbo.ModelVersions mvLe ON mvLe.Id = d.LesionModelVersionId
LEFT JOIN dbo.ModelVersions mvFr ON mvFr.Id = d.FractalModelVersionId
WHERE d.IsVoided = 0 AND i.IsVoided = 0 AND v.IsVoided = 0 AND p.IsVoided = 0
ORDER BY p.Id, v.Id, i.Eye, d.CreatedAt;
