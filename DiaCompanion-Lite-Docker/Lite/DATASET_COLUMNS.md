# Từ điển dữ liệu — dataset nghiên cứu Gap 2

Tải ở web (vai trò **Research**, trang *Kết xuất dữ liệu*) hoặc `docker compose run --rm dbinit export`. **Một dòng = một lần chạy AI của một mắt**, kể cả chưa được bác sĩ xác nhận (dùng `is_reviewed` để lọc). UTF-8 có BOM, dấu phân cách `,`, ô văn bản bọc nháy kép. Đã bỏ họ tên, SĐT, ngày sinh, địa chỉ.

Nhóm cột theo mục đích đánh giá cơ chế deferral hai lớp (Gap 2):

- **Độ tin cậy** (disagreement + thiếu nhánh) → `lesion_implied_grade`, `disagreement`;
- **Nguy cơ nền** (điểm theo BR-04) → `diabetes_type`, `diabetes_duration_years`, `visit_hba1c`, `visit_systolic_bp`, `recheck_months`, `clinical_risk_score`, `clinical_risk_factors`;
- **Deferral** → `base_disagreement_threshold`, `effective_disagreement_threshold`, `is_deferred`, `defer_reason` (xem lưu ý về lưới an toàn bên dưới);
- **Tham chiếu** → `doctor_grade` (nhãn), `review_action`, `grade_distance`.

Từ đây có thể tính risk–coverage (quét ngưỡng trên `disagreement`), so baseline (chỉ disagreement / chỉ mức nặng), và ablation (bỏ lớp nguy cơ).

> **Lưới an toàn, không phải cơ chế hoãn theo bất đồng.** Phần mềm đang chạy quy tắc an toàn: `is_deferred` = 1 khi phân độ máy **hoặc** phân độ suy từ tổn thương ≥ 2, hoặc thiếu một nhánh (`defer_reason` 5 = có thể cần chuyển tuyến, 4 = thiếu nhánh). Điểm nguy cơ **không** ảnh hưởng `is_deferred`, chỉ dùng xếp ưu tiên hàng chờ, nên `effective_disagreement_threshold` luôn bằng `base_disagreement_threshold`. Các cách hoãn đem so trong nghiên cứu (theo bất đồng, theo nguy cơ, kết hợp) được **tính lại ngoài hệ thống** từ `disagreement`, `clinical_risk_score`, `base_disagreement_threshold` theo công thức khóa trước khi thu dữ liệu, ví dụ T_eff = max(T_base − 0,05·min(điểm, 3); 0,05) và hoãn khi `disagreement` ≥ T_eff hoặc thiếu nhánh.

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
| 33 | `effective_disagreement_threshold` | Deferral | Ngưỡng bất đồng ghi lúc chạy. Với quy tắc hiện tại luôn bằng T_base (điểm nguy cơ không đổi ngưỡng); công thức T_eff cho phân tích tính lại ngoài hệ thống. |
| 34 | `base_disagreement_threshold` | Deferral | T_base cấu hình lúc chạy. |
| 35 | `is_deferred` | Deferral | Lưới an toàn: 1 nếu `ai_grade` ≥ 2 hoặc `lesion_implied_grade` ≥ 2, hoặc thiếu nhánh. **Không** phải hoãn theo bất đồng. |
| 36 | `defer_reason` | Deferral | 4 = thiếu nhánh tổn thương; 5 = có thể cần chuyển tuyến. Các mã 1–3 chỉ có ở dữ liệu của bản cũ. |
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
| 54 | `reviewed_by_doctor` | Tham chiếu | Mã bác sĩ xác nhận, dạng `D{Id}` (từ 1.2.2; không xuất tên). Dùng để phân tích theo từng bác sĩ. |
| 55 | `ai_confidence` | Tham khảo | Độ tin cậy **heuristic** (0,5–0,99) suy từ khoảng cách giữa điểm hồi quy và ranh giới độ gần nhất. Model DR là hồi quy nên không có softmax; **không** dùng làm baseline chính, dùng `ai_fold_sd`. |
| 56 | `ai_raw_score` | RQ1 / RQ2 | Điểm hồi quy liên tục của ensemble (trung bình 5 fold × 3 TTA). Ngưỡng độ: 0,481 / 1,468 / 2,286 / 3,469, nên **DR cần chuyển tuyến ⇔ điểm ≥ 1,468**. Dùng làm điểm liên tục cho ROC/AUC. |
| 57 | `ai_fold_scores` | RQ2 | Điểm từng fold, nối bằng `;` (thứ tự fold cố định). |
| 58 | `ai_fold_sd` | Baseline RQ2 | Độ lệch chuẩn (quần thể, chia n) giữa các fold: thước đo bất định của ensemble, baseline "hoãn theo độ bất định" để so với cơ chế deferral. |
| 59 | `image_sha256` | Kiểm soát chất lượng | Mã băm SHA-256 của tệp ảnh. Hai dòng cùng mã = cùng một tệp nạp nhiều lần: giữ một, loại phần còn lại trước khi phân tích. |

> Cột 56–58 chỉ có với lượt chạy AI từ bản **1.2.2**; lượt chạy cũ để trống (chạy lại AI không được vì ảnh đã xác nhận).

> **Từ bản 1.2:** cột `doctor_grade` là **quyết định lâm sàng sau khi đã xem AI**, KHÔNG dùng làm nhãn tham chiếu.
> Nhãn tham chiếu lấy từ `grading.csv` (bên dưới), nối qua `image_id`.

## `grading.csv` — bảng chấm mù (bản 1.2)

Tải ở trang *Kết xuất dữ liệu* (tài khoản Research) hoặc `GET /api/research/grading.csv`. **Mỗi dòng = một ảnh**, kể cả ảnh không chấm được và không chạy AI.

| Cột | Ý nghĩa |
|---|---|
| `image_id` | Khóa nối với `dataset.csv`. |
| `patient_code`, `visit_id`, `eye` | Mã bệnh nhân giả lập, lượt khám, mắt (OD/OS). |
| `quality_status` | 0 chờ, 1 đạt, 2 không đạt. |
| `uploaded_at`, `has_ai` | Thời điểm nạp ảnh; 1 nếu ảnh đã chạy AI. |
| `r1_*` | Vòng 1 — bác sĩ khám chấm mù **trước khi AI chạy**: `gradable` (1/0), `grade` (0–4), `grader` (mã G+Id), `at`. Mã theo Id tài khoản: bác sĩ khám là `G1` ở đây và `D1` ở `dataset.csv`, cùng một người. |
| `r2_*` | Vòng 2 — người chấm độc lập: thấy ảnh và thông tin lâm sàng ẩn danh (tuổi, giới, loại và số năm ĐTĐ, HbA1c, huyết áp, đường huyết), cả trong ứng dụng lẫn gói chấm offline. Bản 1.3.0 chỉ cho xem ảnh; 1.2.x và từ 1.3.1 có thông tin lâm sàng. Không thấy AI, mã bệnh nhân, ngày khám, ghi chú hay nhãn vòng 1. |
| `r3_*` | Vòng 3 — người phân xử, chỉ cho ảnh vòng 1 và 2 bất đồng. |
| `needs_adjudication` | 1 nếu vòng 1 và 2 bất đồng (khác phía ngưỡng chuyển tuyến ≥ 2, hoặc khác chấm được/không) và chưa phân xử. |
| `reference_gradable`, `reference_grade` | **Nhãn tham chiếu**: vòng 3 nếu có; nếu vòng 1 và 2 không bất đồng thì lấy mức nặng hơn; còn thiếu/chưa phân xử thì để trống. |
| `reference_referable` | 1 nếu nhãn tham chiếu ≥ 2 (DR cần chuyển tuyến). |
| `r1_r2_disagree` | 1 nếu vòng 1 và 2 bất đồng (giữ nguyên sau khi phân xử, khác `needs_adjudication`); trống nếu chưa đủ hai vòng. Dùng cho thống kê mức đồng thuận. (1.2.2) |
| `image_sha256` | Như cột 59 của `dataset.csv`. (1.2.2) |
| `r2_source`, `r3_source` | Nguồn phân độ vòng 2 / vòng 3: `app` = chấm trong ứng dụng; `pkg:<mã gói>` = nhập từ gói chấm offline ở máy khác (vd `pkg:V2-20261020-K7QM`); trống nếu chưa có vòng đó. Người chấm (`r2_grader`, `r3_grader`) vẫn là tài khoản Grader được giao gói. (1.3.1) |

### Quy ước chung (cả hai tệp)

- Thời gian: giờ Việt Nam (UTC+7), định dạng ISO 8601 **không** ghi múi giờ.
- Số thập phân dùng dấu chấm. Riêng `clinical_risk_factors` là văn bản hiển thị, có thể có dấu phẩy thập phân ("9,0").
- `eye` ghi chữ (OD/OS); `gender` 0 nam / 1 nữ / 2 khác; `quality_status` 0 chờ / 1 đạt / 2 không đạt; mọi cờ 0/1.
- Phân tích chỉ dùng ảnh có vòng 1 (`r1_gradable` khác trống); ảnh nạp trước bản 1.2 không có chấm mù.

