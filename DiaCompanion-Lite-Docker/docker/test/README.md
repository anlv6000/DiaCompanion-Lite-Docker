# Bộ kiểm thử DR AI-Diagnosis (Docker)

Kiểm thử API end-to-end theo đúng luồng nghiên cứu, chạy trong **project Docker riêng** `dr-ai-diagnosis-test`
(CSDL, ảnh, tài khoản riêng; cổng 8090). Không đụng bản dev (cổng 8080) và không bao giờ chạy trên máy production.
Mỗi lần chạy sinh một **tài liệu kiểm thử Word** trong `test-run/report/`.

## Chạy (Windows, PowerShell tại thư mục gốc repo)

```powershell
Unblock-File .\docker\test\run-tests.ps1
Set-ExecutionPolicy -Scope Process Bypass -Force
.\docker\test\run-tests.ps1 -Tester "Họ tên người test"
```

| Tham số | Tác dụng |
|---|---|
| *(không có)* | Dựng môi trường sạch → chạy 72 case → sinh báo cáo → xóa môi trường |
| `-Keep` | Giữ môi trường tại http://localhost:8090 để làm checklist giao diện; tài khoản ở `test-run/secrets/test_accounts.txt` |
| `-Clean` | Xóa môi trường test còn sót |
| `-PlanOnly` | Chỉ sinh "Kế hoạch và kịch bản kiểm thử" (mọi case ở trạng thái CHƯA CHẠY) |
| `-NoBuild` | Không build lại image (dùng khi mã nguồn không đổi) |
| `-Version 1.3.1` | Số phiên bản ghi trong tài liệu |

Linux/macOS: `docker/test/run-tests.sh [--keep] [--clean] [--plan-only] [--tester "..."]`.

## Yêu cầu

- Docker Desktop đang chạy; thư mục `ai-weights/model_1..3` có trọng số (bộ test chạy AI thật).
- Máy ít RAM: tạm dừng bản dev trước khi chạy (`docker compose stop`), vì hai dịch vụ AI cùng nạp model sẽ tốn bộ nhớ.

## Đầu ra

| Tệp | Nội dung |
|---|---|
| `test-run/report/Bao-cao-kiem-thu-…​.docx` | Tài liệu kiểm thử: phạm vi, môi trường, 72 case (mong đợi/thực tế/trạng thái), lỗi, checklist giao diện và bộ cài, ô ký xác nhận |
| `test-run/report/results.json`, `results.csv` | Kết quả thô |
| `test-run/secrets/` | Mật khẩu tạm và tài khoản của môi trường test (dữ liệu giả) |

Mã thoát của script: 0 khi tất cả ĐẠT, khác 0 khi có case LỖI/BỊ CHẶN.

## Cấu trúc

- `cases.py` — danh mục test case (thêm case mới bằng `@case(...)`; các case chạy tuần tự, dùng chung `ctx.s`).
- `run_tests.py` — khung chạy; `report_docx.py` — sinh tài liệu Word và hai checklist thủ công.
- `fixtures/` — ảnh đáy mắt **tổng hợp** (không phải ảnh bệnh nhân). Muốn dùng ảnh thật đã ẩn danh: thay hai tệp cùng tên.
- `docker-compose.test.yml` — phần ghi đè cho project test; `Dockerfile` — image chạy test.

## Phạm vi

Tự động: môi trường, đăng nhập/mật khẩu, phân quyền, hồ sơ ẩn danh, lượt khám, nạp ảnh (kể cả chặn nạp trùng cùng mắt khi bấm hai lần), chấm mù 3 vòng, chạy AI hai mắt đồng thời,
thông tin lâm sàng, AI, xác nhận lâm sàng, kết xuất CSV (kể cả đối chiếu số liệu và mã băm ảnh),
gói chấm offline cho máy 2/3 (tạo, hủy, nội dung zip không lộ thông tin, nhập kết quả, nhập lại, tệp bị sửa, phân quyền).
Thủ công (in trong tài liệu, mục 7–8): thao tác giao diện và bộ cài Windows.
