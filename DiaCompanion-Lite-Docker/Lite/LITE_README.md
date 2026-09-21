# DiaCompanion — bản "Thu thập dữ liệu" (Lite)

Bản rút gọn của hệ thống chính thức, chỉ giữ luồng thu thập dữ liệu nghiên cứu:

**tạo bệnh nhân → nhập chỉ số → nạp ảnh đáy mắt 2 mắt → chạy AI → bác sĩ xác nhận → đóng lượt.**

Backend, cơ sở dữ liệu, dịch vụ AI, quy tắc nghiệp vụ và trang xem ảnh `/fundus/:id` là **của bản chính thức, không viết lại**. Bản Lite chỉ bỏ bớt màn hình/endpoint và thêm một giao diện gọn.

## Có gì / không có gì

**Hai loại tài khoản:**

| Tài khoản | Vai trò trong DB | Làm được |
|---|---|---|
| Lâm sàng (bác sĩ làm tất cả) | `Doctor` + `Receptionist` | tạo/sửa bệnh nhân, lượt khám, nhập chỉ số, nạp ảnh 2 mắt, chạy AI, xác nhận, xem kết quả/diễn tiến/chỉ số, đóng lượt |
| Nghiên cứu | `Research` | **chỉ** tải dataset CSV (không xem hồ sơ bệnh nhân, không có quyền lâm sàng) |

| Giữ | Bỏ |
|---|---|
| Đăng nhập nhân viên, đổi mật khẩu | Kê đơn / thuốc / tuân thủ thuốc |
| Tạo/sửa/tìm bệnh nhân (không cấp tài khoản) | Ứng dụng bệnh nhân (mobile), OTP, SMS |
| Lượt thu thập: HbA1c, huyết áp, đường huyết | Quản trị tài khoản/cấu hình/model registry, lễ tân riêng, ca trực |
| Nạp ảnh OD + OS, duyệt chất lượng, chạy 3 model AI | Triage queue, tái tầm soát, thống kê tổng quan, blog, feedback |
| Trang xem ảnh: lesion, vessel/fractal, deferral, approve/override | Xuất báo cáo PDF, nhắc thuốc/tái khám tự động |
| **Kết quả đã xác nhận** (bảng, mở chi tiết ảnh) | |
| **Diễn tiến** DR / fractal / HbA1c theo thời gian | |
| **Chỉ số theo thời gian** (HbA1c, đường huyết, huyết áp theo lượt) | |
| **Kết xuất dataset CSV** cho Gap 2 (vai trò Research) | |

Trang bệnh nhân có 4 tab: *Lượt hiện tại · Kết quả đã xác nhận · Diễn tiến · Chỉ số theo thời gian*.

**Vì sao vẫn có "xác nhận của bác sĩ" và "đóng lượt":** backend không cho đóng lượt khi còn kết quả AI chưa được bác sĩ approve/override, và không cho tạo lượt mới khi bệnh nhân còn lượt mở. Đây cũng là chỗ ghi **phân độ tham chiếu** (`doctor_grade`) — nhãn quan trọng nhất của nghiên cứu.

**Điểm nguy cơ trong bản Lite** chỉ dùng: HbA1c, số năm mắc bệnh, huyết áp (chỉ Type 2, cần ≥ 3 lần đo hợp lệ ⇒ ≥ 3 lượt) và quá hạn tái khám. Tuân thủ thuốc không có (đã bỏ đơn thuốc) nên tính 0 điểm theo BR-04 ("thiếu dữ liệu = 0 điểm").

## Cài đặt (một máy Windows trong bệnh viện)

Cần: SQL Server (Express được), .NET 8 Hosting Bundle, Python 3.10 + `pip install -r ai_service/requirements.txt`, **trọng số AI** (không có trong git — xem `ai_service/HUONG_DAN_MODEL_VERSION.md`), Node 18+ chỉ để build web.

1. **CSDL.** Repo *không* có script tạo schema từ đầu (`Backend/DiaCompanion/Database/` chỉ là các patch). Xuất schema-only từ SQL Server đang chạy của bạn (SSMS → Generate Scripts → *Schema only*) hoặc restore bản sao trống của DB hiện có, rồi tạo DB `DiaCompanion`.
2. **Khởi tạo dữ liệu nền + tài khoản nghiên cứu:**
   ```
   cd Lite
   python seed_lite.py init --email bacsi@benhvien.local --research-email nghiencuu@benhvien.local --ai-service-dir ..\ai_service
   sqlcmd -S .\SQLEXPRESS -d DiaCompanion -i lite_seed.sql
   ```
   Script in **một lần** mật khẩu tạm; lần đăng nhập đầu bắt buộc đổi. Tài khoản bác sĩ có đủ hai vai trò Doctor + Receptionist (bản Lite yêu cầu cả hai); `--research-email` tạo thêm tài khoản Research. DB đã seed từ trước: `python seed_lite.py add-research --email …`. Quên mật khẩu: `python seed_lite.py reset-password --email …` rồi chạy file `lite_reset_password.sql`. Xóa `lite_seed.sql` sau khi chạy.
3. **Backend:** sao `Lite/appsettings.Lite.example.json` thành `appsettings.json` (file này bị `.gitignore`), sửa đường dẫn; đặt biến môi trường `JWT__SIGNINGKEY` (chuỗi ngẫu nhiên dài). `dotnet publish -c Release`, chạy trên IIS (app pool *No Managed Code*) hoặc Kestrel. Kiểm tra `/health`.
4. **AI service:** đặt `FUNDUS_ROOT` = thư mục **cha** của `fundus` (vd `D:\DiaCompanion\storage`) và `MASK_OUTPUT_ROOT`; chạy `uvicorn app:app --host 127.0.0.1 --port 8000`. Chỉ cho backend gọi.
5. **Web:** `cd Frontend && npm ci && npm run build:lite` → thư mục `dist-lite/`. Sao `Lite/config.js.example` đè lên `dist-lite/config.js`. Phục vụ bằng IIS có reverse proxy `/api/*` về backend (khi đó để `""` và không cần CORS), hoặc chạy qua Electron: `set DIACOMPANION_DIST=dist-lite`.
6. **Thử nhanh:** đăng nhập → *Thêm bệnh nhân* → *Bắt đầu lượt thu thập* → nhập chỉ số → nạp ảnh OD/OS → *Đạt chất lượng* → *Chạy AI* → *Chi tiết & xác nhận* → *Đóng lượt*.

## Lấy số liệu (Gap 2)

- **Web:** đăng nhập tài khoản Research → *Kết xuất dữ liệu* → chọn khoảng ngày (tùy chọn) → *Tải dataset CSV*. Endpoint `GET /api/research/dataset.csv` (chỉ `Research`/`Admin`), mỗi lần tải được ghi audit.
- **Dòng lệnh:** `docker compose run --rm dbinit export` → `./exports/dataset.csv` (cùng 54 cột, sinh bằng SQL).
- **Từ điển 54 cột:** `DATASET_COLUMNS.md`. Mỗi dòng = một lần chạy AI của một mắt (kể cả chưa xác nhận), gồm biến nguy cơ nền, disagreement, ngưỡng hiệu dụng, deferral, fractal, phiên bản model và phân độ tham chiếu của bác sĩ. Đã bỏ họ tên/SĐT/ngày sinh/địa chỉ.

## Chưa được kiểm chứng — làm trước khi giao

- **Backend chưa được compile** (môi trường soạn thảo không có .NET SDK/NuGet). Thay đổi C# chỉ gồm: xóa 9 controller không dùng và tắt một dòng đăng ký worker. Chạy `dotnet build` để chắc chắn. Project `DiaCompanion.Tests` có test gọi các endpoint đã xóa — không thuộc gói giao.
- `seed_lite.py` đã chạy thử phần sinh SQL và định dạng băm mật khẩu, **chưa chạy SQL trên SQL Server**; `export_dataset.sql` cũng chưa chạy thử (đã kiểm tra thứ tự 54 cột khớp với CSV của web; nên so vài dòng hai đường).
- Endpoint `/api/research/dataset.csv` và các thay đổi C# mới **mới qua kiểm tra cú pháp (tree-sitter), chưa qua compiler** — chạy `dotnet build`.
- Giao diện Lite đã qua `tsc`, build (`dist-lite`) và một smoke test jsdom với API giả (9 ca: các tab, phân quyền Doctor/Research, tải CSV; `Lite/tests/`); **chưa chạy với backend + AI thật**.
- Phải chạy thử end-to-end trên máy có backend, SQL Server và trọng số trước khi đưa vào bệnh viện.
- Lỗi có sẵn ở bản chính thức (không sửa ở đây): `referralTypes` trong `Frontend/src/lib/enums.ts` chỉ có 3 nhãn trong khi enum `ReferralType` của backend có 4 giá trị (nhãn bị lệch từ giá trị 1). Bản Lite dùng danh sách 4 nhãn riêng.

## Lưu ý đạo đức nghiên cứu

Vai trò Research không đọc được hồ sơ bệnh nhân qua API (mọi endpoint lâm sàng chỉ nhận Doctor/Receptionist/Admin); CSV chỉ giữ mã bệnh nhân giả lập và tuổi.

Hồ sơ vẫn bắt buộc họ tên, ngày sinh, SĐT (ràng buộc của backend). Nếu nghiên cứu cần dữ liệu ẩn danh ngay từ khâu nhập, cần sửa DTO/service ở backend (chưa làm).
