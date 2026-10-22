# DR AI-Diagnosis: bộ cài Windows cho bệnh viện

Một tệp `Setup.exe` cài **offline** toàn bộ: SQL Server Express, database, backend + web, AI (Python + trọng số), dịch vụ Windows và công cụ vận hành. Bản Docker vẫn giữ nguyên, không bị ảnh hưởng.

Từ bản 1.3.1 phần mềm đổi tên từ "DiaCompanion Lite" thành **DR AI-Diagnosis** (xem mục 6 về nâng cấp từ bản cũ).

## 1. Chuẩn bị máy build (làm một lần)

| Cần cài | Ghi chú |
|---|---|
| .NET 8 SDK | `dotnet --version` ra 8.x |
| Node.js 20 LTS | `npm --version` |
| Inno Setup 6 | Mặc định ở `C:\Program Files (x86)\Inno Setup 6\ISCC.exe` |
| Internet | Chỉ cần khi build; máy bệnh viện không cần |

Đặt các tệp không nằm trong git:

```
installer\payload\sqlserver\SQLEXPR_x64_ENU.exe   ← SQL Server 2022 Express (Download Media → Express Core)
ai-weights\model_1\efficientnet_b4_fold*_best.pth
ai-weights\model_2\tjdr_unet_v4_2_best.pth
ai-weights\model_3\best_model.keras
```

Tạo Python runtime cho AI (tải Python 3.11 và cài thư viện theo `requirements.lock.txt`, khoảng 10–20 phút):

```powershell
cd <thư mục repo>
Set-ExecutionPolicy -Scope Process Bypass
.\installer\scripts\prepare-ai-runtime.ps1          # mặc định: C:\Build\DiaCompanion\ai-runtime
```

Thư mục runtime giữ tên cũ để máy build đã chuẩn bị không phải làm lại. Chạy lại lệnh này khi đổi `requirements.lock.txt`. Muốn làm lại từ đầu thì thêm `-Force`.

## 2. Build bản release

```powershell
cd <thư mục repo>
Set-ExecutionPolicy -Scope Process Bypass
.\installer\scripts\build-installer.ps1 -Version 1.3.1
```

Kết quả nằm ở `release\DR-AI-Diagnosis-Setup-x64-1.3.1.exe`. Script in ra dung lượng và mã SHA256 của tệp.

Lần đầu, script tự tải WinSW (wrapper chạy Windows Service) và VC++ runtime vào `installer\payload\`. Tham số tùy chọn: `-AiRuntime <thư mục>`, `-SqlServerInstaller <tệp>`, `-OutputRoot <thư mục>`.

Nếu muốn dùng đúng schema của DB đang chạy thay cho schema sinh từ EF Core: xuất *schema-only* từ SSMS, lưu thành `Backend\DiaCompanion\Database\lite_schema.sql`, script sẽ tự dùng tệp đó.

## 3. Cài ở bệnh viện

1. Chạy `Setup.exe` bằng quyền Administrator trên Windows 10/11 x64. Máy cần RAM ≥ 8 GB và ổ đĩa trống ≥ 15 GB.
2. Nhập email bác sĩ, họ tên, email tài khoản nghiên cứu và cổng web (mặc định 8080).
3. Chờ 10–25 phút để cài SQL Server, database và dịch vụ.
4. Trang **Kết quả cài đặt** hiện địa chỉ truy cập (máy này và các máy trong LAN) cùng **mật khẩu tạm**. Ghi lại, rồi xóa tệp `C:\ProgramData\DRAIDiagnosis\config\initial_credentials.txt`.

**Cài đè / nâng cấp:** chạy `Setup.exe` bản mới lên máy đã cài. Bộ cài tự nhận ra bản cũ, giữ nguyên database, ảnh, tài khoản và cổng, và không hỏi lại cấu hình. Mỗi lần cài, các tệp `db\migrations\*.sql` (idempotent) được áp để cập nhật cấu trúc database.

**Cài lỗi giữa chừng:** sửa nguyên nhân (xem log), rồi chạy lại đúng `Setup.exe` đó. Bộ cài dùng lại mật khẩu đã tạo và làm tiếp các bước còn thiếu.

## 4. Vận hành (Start Menu → DR AI-Diagnosis)

| Mục | Việc |
|---|---|
| Mở DR AI-Diagnosis | Mở giao diện web trên máy này |
| Kiểm tra hệ thống | Trạng thái 2 service, AI, backend + DB, web; in địa chỉ LAN |
| Sao lưu dữ liệu | `DRAIDiagnosis.bak` + `storage.tar.gz` vào `C:\ProgramData\DRAIDiagnosis\backups\<ngày-giờ>\` |
| Cấp lại mật khẩu | Nhập email → in mật khẩu tạm mới |
| Thêm người chấm độc lập | Tạo tài khoản vai trò Grader (chấm mù vòng 2/3, trong ứng dụng hoặc qua gói chấm) |
| Thêm tài khoản nghiên cứu | Tạo tài khoản vai trò Research (tải dữ liệu, tạo và nhập gói chấm) |
| Khởi động lại dịch vụ | Dừng/chạy lại AI + backend rồi kiểm tra |
| Mở thư mục log | `installer.log`, log từng service (`DRAIDiagnosisAI.*.log`, `DRAIDiagnosisBackend.*.log`) |

### Chấm mù trên nhiều máy (1.3.1)

Máy cài bộ cài (máy 1) giữ toàn bộ dữ liệu: bác sĩ khám chấm mù vòng 1 rồi chạy AI như cũ. Máy 2 (vòng 2) và máy 3 (vòng 3, phân xử) **không cần cài gì**, không cần mạng:

1. Ở máy 1, đăng nhập tài khoản nghiên cứu, mở **Chấm nhiều máy**, chọn vòng và người chấm, bấm **Tạo gói và tải về**. Gói là tệp zip gồm ảnh đáy mắt (đã bỏ metadata, đặt tên ngẫu nhiên, xáo thứ tự), thông tin lâm sàng ẩn danh như trang chấm trong ứng dụng, và trang chấm. Gói không có mã bệnh nhân, ngày khám, ghi chú, kết quả AI hay phân độ vòng trước.
2. Chép zip sang máy chấm (USB), giải nén toàn bộ, mở `index.html` bằng Chrome hoặc Edge, chấm (phím 0–4, K = không chấm được). Tiến độ tự lưu trong trình duyệt.
3. Bấm **Xuất kết quả**, chép tệp `KET-QUA-….json` về máy 1, nhập ở mục **Nhập kết quả**. Nhập lại cùng tệp không ghi trùng; tệp bị sửa tay bị từ chối.

Ảnh đang nằm trong gói mở không xuất hiện ở hàng chờ trong ứng dụng và không vào gói khác cùng vòng. Hủy gói thì ảnh chưa có kết quả trở lại hàng chờ. Cột `r2_source`, `r3_source` của `grading.csv` ghi nguồn phân độ (`app` hoặc `pkg:<mã gói>`).

## 5. Bố cục sau khi cài

- `C:\Program Files\DR AI-Diagnosis\` chứa `backend\` (kèm `wwwroot\` là giao diện, tệp chạy `DRAIDiagnosis.exe`), `ai\`, `services\` (WinSW), `scripts\`, `db\`.
- `C:\ProgramData\DRAIDiagnosis\` chứa:
  - `config\`: `secrets.json`, `installer.json`. Chỉ SYSTEM và Administrators đọc được.
  - `storage\fundus`, `storage\ai_masks`: ảnh gốc và mask AI.
  - `logs\`, `backups\`.
- Database `DRAIDiagnosis` trên `.\SQLEXPRESS`; SQL Server tắt TCP, chỉ nhận kết nối từ chính máy này.
- Service `DRAIDiagnosisAI` nghe `127.0.0.1:8000`, chỉ trong máy. Service `DRAIDiagnosisBackend` nghe `0.0.0.0:<cổng>`; firewall (quy tắc "DR AI-Diagnosis Web") chỉ mở cổng này cho các máy cùng subnet LAN.

Gỡ cài đặt sẽ xóa ứng dụng và service, nhưng **giữ** SQL Server, database và `C:\ProgramData\DRAIDiagnosis`. Cài lại sau đó sẽ được tính là cài đè.

## 6. Nâng cấp từ DiaCompanion Lite (≤ 1.3.0) lên 1.3.1

Chạy `DR-AI-Diagnosis-Setup-x64-1.3.1.exe` như cài đè. Bộ cài tự làm, chỉ một lần:

1. Dừng và gỡ service cũ `DiaCompanionAI`, `DiaCompanionBackend`, xóa quy tắc firewall cũ.
2. Sao lưu database `DiaCompanion` (bản sao lưu nằm ở `C:\ProgramData\DRAIDiagnosis\backups\truoc-doi-ten-<ngày-giờ>\` sau khi chuyển) rồi **đổi tên** thành `DRAIDiagnosis` (không chép dữ liệu, không mất gì).
3. Chuyển `storage\` và `backups\` sang `C:\ProgramData\DRAIDiagnosis\`; chép `config\` (giữ mật khẩu, khóa JWT, cổng) và `logs\` (vào `logs\diacompanion-1.3.0\`).
4. Đổi tên thư mục cũ thành `C:\ProgramData\DiaCompanion.da-chuyen-<ngày-giờ>` và ghi dấu `config\migrated-from-diacompanion.txt`. Kiểm tra phần mềm chạy bình thường vài ngày rồi tự xóa thư mục này.
5. Sau khi dịch vụ mới chạy được, xóa `C:\Program Files\DiaCompanion Lite\` và lối tắt Start Menu cũ.

Tài khoản, mật khẩu và địa chỉ truy cập giữ nguyên. Phiên đăng nhập cũ hết hiệu lực (đổi tên JWT), người dùng đăng nhập lại.

**Ảnh nạp trùng cùng mắt** (lỗi bấm hai lần ở bản ≤ 1.3.0, gây thông báo "còn 1/3 kết quả AI chưa xác nhận"): trang kết quả cài đặt in số lượt bị ảnh hưởng. Mở từng lượt trên giao diện, mục **Ảnh nạp trùng**, thu hồi ảnh thừa. Bản 1.3.1 chặn nạp trùng ở ứng dụng; chỉ mục duy nhất trong database được tạo ở lần cài đè sau khi đã hết ảnh trùng.
