# Từ điển dữ liệu — dataset nghiên cứu Gap 2

Tải ở web (vai trò **Research**, trang *Kết xuất dữ liệu*) hoặc `docker compose run --rm dbinit export`. **Một dòng = một lần chạy AI của một mắt**, kể cả chưa được bác sĩ xác nhận (dùng `is_reviewed` để lọc). UTF-8 có BOM, dấu phân cách `,`, ô văn bản bọc nháy kép. Đã bỏ họ tên, SĐT, ngày sinh, địa chỉ.

Nhóm cột theo mục đích đánh giá cơ chế deferral hai lớp (Gap 2):

- **Độ tin cậy** (disagreement + thiếu nhánh) → `lesion_implied_grade`, `disagreement`;
- **Nguy cơ nền** (điểm theo BR-04) → `diabetes_type`, `diabetes_duration_years`, `visit_hba1c`, `visit_systolic_bp`, `recheck_months`, `clinical_risk_score`, `clinical_risk_factors`;
- **Deferral** → `base_disagreement_threshold`, `effective_disagreement_threshold`, `is_deferred`, `defer_reason`;
- **Tham chiếu** → `doctor_grade` (nhãn), `review_action`, `grade_distance`.

Từ đây có thể tính risk–coverage (quét ngưỡng trên `disagreement`), so baseline (chỉ disagreement / chỉ mức nặng), và ablation (bỏ lớp nguy cơ).

**Lưu ý:** tuân thủ thuốc không có trong bản này (đã bỏ đơn thuốc) nên điểm nguy cơ không có thành phần đó; “quá hạn tái khám” chỉ suy được từ `recheck_months` và ngày khám các lượt trước. Cột Confidence cũ không xuất (đã rút khỏi cơ chế).

| # | Cột | Nhóm | Ý nghĩa |
|---|---|---|---|
| 1 | `ai_diagnosis_id` | Khóa dòng | Id lần chạy AI (một mắt). |
| 2 | `patient_code` | Bối cảnh | Mã bệnh nhân giả lập (không phải định danh). |
| 3 | `gender` | Bối cảnh | 0 Nam, 1 Nữ, 2 Khác. |
| 4 | `age_at_visit` | Bối cảnh | Tuổi tròn tại ngày khám. |
| 5 | `diabetes_type` | Nguy cơ nền | 0 không xác định, 1 Type 1, 2 Type 2, 3 thai kỳ — quyết định luật điểm nguy cơ. |
| 6 | `diabetes_duration_years` | Nguy cơ nền | Số năm mắc. Điểm nguy cơ: T1 <5 năm 0đ, 5–14 năm +1, ≥15 năm +2; T2 ≥10 năm +1. |
| 7 | `baseline_hba1c` | Bối cảnh | HbA1c nền ghi ở hồ sơ (%). |
| 8 | `visit_id` | Khóa dòng | Id lượt khám. |
| 9 | `visit_date` | Bối cảnh | Ngày giờ khám (giờ Việt Nam, ISO 8601). |
| 10 | `visit_status` | Bối cảnh | 0 đang khám, 1 đã đóng. |
| 11 | `recheck_months` | Nguy cơ nền | Số tháng tái khám bác sĩ đặt khi đóng lượt (nguồn cho “quá hạn tái khám”). |
| 12 | `visit_hba1c` | Nguy cơ nền | HbA1c đo trong lượt (%): +1/+2 điểm khi ≥ 8 / ≥ 9. |
| 13 | `visit_glucose_mmol_l` | Bối cảnh | Đường huyết (mmol/L) — KHÔNG dùng trong điểm nguy cơ. |
| 14 | `visit_systolic_bp` | Nguy cơ nền | HA tâm thu (mmHg); chỉ Type 2, trung bình 3 lần đo gần nhất ≥ 140 → +1. |
| 15 | `visit_diastolic_bp` | Bối cảnh | HA tâm trương (mmHg). |
| 16 | `image_id` | Khóa dòng | Id ảnh đáy mắt. |
| 17 | `eye` | Khóa dòng | OD mắt phải / OS mắt trái. |
| 18 | `quality_status` | Bối cảnh | 0 chờ duyệt, 1 đạt, 2 không đạt (chỉ ảnh đạt mới chạy AI). |
| 19 | `ai_run_at` | Bối cảnh | Thời điểm chạy AI (giờ Việt Nam). |
| 20 | `ai_grade` | Kết quả AI | Phân độ DR nhánh phân loại (0–4, ICDR). |
| 21 | `lesion_implied_grade` | Độ tin cậy | Phân độ suy ra từ tổn thương nhánh phân vùng (0–4). |
| 22 | `count_ma` | Kết quả AI | Số tổn thương vi phình mạch. |
| 23 | `count_he` | Kết quả AI | Số xuất huyết. |
| 24 | `count_ex` | Kết quả AI | Số xuất tiết cứng. |
| 25 | `count_se` | Kết quả AI | Số xuất tiết mềm. |
| 26 | `area_ma` | Kết quả AI | Diện tích MA. |
| 27 | `area_he` | Kết quả AI | Diện tích HE. |
| 28 | `area_ex` | Kết quả AI | Diện tích EX. |
| 29 | `area_se` | Kết quả AI | Diện tích SE. |
| 30 | `disagreement` | Độ tin cậy | D = |ai_grade − lesion_implied_grade| / 4 (0…1). Rỗng nếu thiếu nhánh. |
| 31 | `clinical_risk_score` | Nguy cơ nền | Điểm nguy cơ nền tại thời điểm chạy (đã lưu, không tính lại). |
| 32 | `clinical_risk_factors` | Nguy cơ nền | Các yếu tố đã cộng điểm (văn bản). |
| 33 | `effective_disagreement_threshold` | Deferral | T_eff = max(T_base − 0.05·min(điểm,3), 0.05) đã áp dụng. |
| 34 | `base_disagreement_threshold` | Deferral | T_base cấu hình lúc chạy. |
| 35 | `is_deferred` | Deferral | 1 nếu D ≥ T_eff hoặc thiếu nhánh. |
| 36 | `defer_reason` | Deferral | Mã lý do chuyển bác sĩ. |
| 37 | `fractal_dimension` | Bổ trợ | FD toàn ảnh — chỉ theo dõi dọc, không dùng cho deferral. |
| 38 | `fractal_st` | Bổ trợ | FD cung thái dương trên. |
| 39 | `fractal_sn` | Bổ trợ | FD cung mũi trên. |
| 40 | `fractal_it` | Bổ trợ | FD cung thái dương dưới. |
| 41 | `fractal_in` | Bổ trợ | FD cung mũi dưới. |
| 42 | `fractal_asymmetry` | Bổ trợ | FD_asym. |
| 43 | `fractal_tn` | Bổ trợ | FD_TN. |
| 44 | `lacunarity` | Bổ trợ | Lacunarity. |
| 45 | `dr_model_version` | Phiên bản | Model phân loại. |
| 46 | `lesion_model_version` | Phiên bản | Model phân vùng tổn thương. |
| 47 | `fractal_model_version` | Phiên bản | Model mạch máu/fractal. |
| 48 | `is_reviewed` | Tham chiếu | 1 nếu bác sĩ đã xác nhận (không xác nhận = rỗng ở các cột dưới). |
| 49 | `review_action` | Tham chiếu | 0 phê duyệt AI, 1 ghi đè. |
| 50 | `doctor_grade` | Tham chiếu | Phân độ cuối của bác sĩ = nhãn tham chiếu. |
| 51 | `grade_distance` | Tham chiếu | |doctor_grade − ai_grade|. |
| 52 | `override_reason` | Tham chiếu | Lý do ghi đè (văn bản tự do). |
| 53 | `reviewed_at` | Tham chiếu | Thời điểm xác nhận (giờ Việt Nam). |
| 54 | `reviewed_by_doctor` | Tham chiếu | Tên bác sĩ xác nhận (định danh nhân viên, không phải bệnh nhân). |
