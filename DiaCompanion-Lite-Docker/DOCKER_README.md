# DiaCompanion Lite — bản build Docker

Một lệnh dựng toàn bộ hệ thống thu thập dữ liệu trên **một máy** (Windows + Docker Desktop, hoặc Linux + Docker Engine):

```
SQL Server 2022 Express ← ASP.NET Core 8 backend ← nginx (web Lite + reverse proxy /api) ← trình duyệt
                                  ↓ HTTP nội bộ
                           AI service (FastAPI, CPU)  ── chung volume ảnh với backend
```

| Container | Vai trò | Ra ngoài |
|---|---|---|
| `db` | SQL Server 2022 Express (dữ liệu ở volume `mssql_data`) | không |
| `dbinit` | chạy **một lần**: tạo DB, áp schema, seed tài khoản + 3 model | không |
| `backend` | API .NET 8 (đã bỏ các module không dùng) | không (qua nginx) |
| `ai` | 3 model DR / lesion / vessel-fractal, chạy CPU | không |
| `web` | nginx: giao diện Lite + chuyển tiếp `/api/*` | **cổng `WEB_PORT` (mặc định 8080)** |

## Yêu cầu

- Docker Desktop 4.x (WSL2) hoặc Docker Engine + Compose v2; **RAM ≥ 8 GB** cấp cho Docker, đĩa trống ≥ 15 GB (image AI có PyTorch + TensorFlow).
- Máy build cần Internet (NuGet, npm, PyPI, PyTorch CPU wheels, Docker Hub). Sau khi build xong, chạy được **offline** trong LAN.
- **Trọng số AI** (không có trong git) đặt vào:
  ```
  ai-weights/model_1/efficientnet_b4_fold0_best.pth … fold4_best.pth   (1–5 fold)
  ai-weights/model_2/tjdr_unet_v4_2_best.pth
  ai-weights/model_3/best_model.keras
  ```

## Chạy

```powershell
# Windows PowerShell                          # Linux/macOS
.\docker\generate-env.ps1                     # ./docker/generate-env.sh
docker compose up -d --build
```
(Hoặc `cp .env.example .env` và tự sửa. Mật khẩu SA chỉ dùng chữ + số để khỏi hỏng chuỗi kết nối.)

Lần đầu build mất vài chục phút. Sau đó:

1. Mở `http://<địa-chỉ-máy>:8080`.
2. Mật khẩu tạm của `LITE_ADMIN_EMAIL` (bác sĩ) và `LITE_RESEARCH_EMAIL` (nghiên cứu) nằm trong `./secrets/initial_credentials.txt` (cũng in ở `docker compose logs dbinit`). Đăng nhập → bắt buộc đổi mật khẩu → **xóa tệp đó**.
3. Bác sĩ: *Thêm bệnh nhân → Bắt đầu lượt → nhập chỉ số → nạp ảnh OD/OS → Đạt chất lượng → Chạy AI → Chi tiết & xác nhận → Đóng lượt*; xem lại ở các tab *Kết quả đã xác nhận / Diễn tiến / Chỉ số theo thời gian*.
4. Nghiên cứu viên: đăng nhập tài khoản Research → *Kết xuất dữ liệu* → tải CSV (từ điển cột: `Lite/DATASET_COLUMNS.md`).

Kiểm tra: `docker compose ps` (backend/web phải *healthy*), `docker compose logs -f backend ai`.

## Vận hành

| Việc | Lệnh |
|---|---|
| Xuất dataset Gap 2 (CSV, đã bỏ định danh) | Web (tài khoản Research) hoặc `docker compose run --rm dbinit export` → `./exports/dataset.csv` |
| Thêm tài khoản Research vào DB cũ | đặt `LITE_RESEARCH_EMAIL` trong `.env` rồi `docker compose run --rm dbinit` (không đụng tài khoản bác sĩ) |
| Cấp lại mật khẩu tạm | `docker compose run --rm dbinit reset-password` |
| Dừng / chạy lại | `docker compose stop` / `docker compose up -d` (dữ liệu nằm trong volume, không mất) |
| Cập nhật code | `git pull` (hoặc thay mã nguồn) rồi `docker compose up -d --build` |
| Sao lưu DB | `docker compose exec db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "<SA>" -Q "BACKUP DATABASE DiaCompanion TO DISK='/var/opt/mssql/dia.bak' WITH INIT"` rồi `docker compose cp db:/var/opt/mssql/dia.bak ./dia.bak` |
| Sao lưu ảnh + mask | `docker run --rm -v diacompanion-lite_storage:/d -v "${PWD}:/b" alpine tar czf /b/storage.tgz -C /d .` |
| **Xóa sạch (mất dữ liệu)** | `docker compose down -v` |

## Schema CSDL

Repo không có script tạo schema đầy đủ (chỉ các patch trong `Backend/DiaCompanion/Database/`). Khi build, image sinh schema bằng `dotnet ef dbcontext script` **từ mô hình EF Core (`AppDbContext`)**. Nếu bạn muốn dùng đúng schema của DB đang chạy: xuất *schema-only* từ SSMS và lưu thành `Backend/DiaCompanion/Database/lite_schema.sql` trước khi build — Dockerfile sẽ dùng tệp đó thay cho bản sinh tự động. Schema tự sinh **không** gồm các CHECK constraint thêm bằng patch SQL (vd. `CK_ModelVersions_ModelType`).

## Bảo mật (mạng nội bộ)

- Chỉ cổng web được publish; SQL Server, backend, AI không mở ra máy chủ. **Chưa có HTTPS** — dùng trong LAN kín, hoặc đặt sau reverse proxy có TLS của bệnh viện.
- Đã bỏ dòng `Console.WriteLine(connectionString)` trong `Program.cs` (nó in mật khẩu SA ra log).
- Giới hạn đăng nhập của backend là 5 lần/phút **theo IP**; sau nginx mọi người dùng chung một IP nội bộ nên giới hạn này áp chung cho cả nhóm.
- `.env`, `secrets/`, `exports/` đã nằm trong `.gitignore`. Đừng commit.

## Chưa kiểm chứng (môi trường soạn thảo không có Docker/.NET)

Đã kiểm tra: nginx (`nginx -t` + chạy thật với backend giả: proxy `/api`, upload 5 MB qua, 13 MB bị 413, SPA fallback, header cache), luồng `dbinit.sh` với `sqlcmd` giả (init lần 1/lần 2 bỏ qua, export, reset, email sai, thiếu trọng số), shellcheck, tham chiếu compose và các đường dẫn `COPY`, bản web Lite build được.

**Chưa chạy:** `docker compose build/up`; build lại backend sau khi thêm `ResearchController`/`ExportService.ResearchDatasetCsv` (mới qua kiểm tra cú pháp); build .NET và `dotnet ef dbcontext script`; cài đặt image AI (PyTorch/TensorFlow); SQL sinh ra trên SQL Server thật; chạy AI với trọng số thật.

Điểm dễ hỏng nhất khi build lần đầu:
1. **TensorFlow/Keras:** mặc định `tensorflow-cpu==2.15.1`. Nếu `model_3` nạp lỗi (huấn luyện bằng Keras 3): `docker compose build --build-arg TF_VERSION=2.16.2 ai`.
2. **`dotnet ef dbcontext script`** cần khởi tạo host từ `Program.cs`; nếu lỗi, dùng `lite_schema.sql` như trên.
3. **Thời gian suy luận CPU** có thể dài; timeout backend→AI đặt 300 s, nginx 300 s.
