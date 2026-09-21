# Hướng dẫn tích hợp Model AI vào DiaCompanion

Bản thử nghiệm: dùng 3 model đầu (DR grading, Lesion, Fractal). Weight nạp sau.
Kiến trúc: **dịch vụ Python FastAPI** phơi 3 endpoint, **backend C# gọi 3 lần
rồi gộp**. Backend gần như không đổi (đã sẵn cơ chế gọi AI qua HTTP).

---

## Tổng quan luồng

```
Bác sĩ bấm "Chạy AI"
   → DiagnosesService.RunAsync(image.FilePath, model.FilePath)
      → AiInferenceClient  gọi 3 endpoint:
           POST http://localhost:8000/infer/dr
           POST http://localhost:8000/infer/lesion
           POST http://localhost:8000/infer/fractal
      → gộp thành 1 AiInferenceResponse
   → lưu AiDiagnosis vào DB (giữ nguyên logic cũ)
```

---

## PHẦN 1 — Dịch vụ Python (thư mục mới `ai_service/`)

Đặt cả thư mục `ai_service/` **ngang hàng** với thư mục `Backend/` trong repo:

```
<repo>/
├── Backend/
│   └── DiaCompanion/...
└── ai_service/              ← ĐẶT MỚI Ở ĐÂY
    ├── app.py
    ├── model_runners.py
    ├── requirements.txt
    ├── .env.example
    ├── README.md
    └── models/
        ├── model_1/predict.py + weights/
        ├── model_2/predict.py + weights/
        └── model_3/predict.py + weights/
```

Các file `predict.py` chính là 3 file bạn đã có, giữ nguyên không sửa.

### Chạy thử

```bash
cd ai_service
python -m venv venv && source venv/bin/activate   # Windows: venv\Scripts\activate
pip install -r requirements.txt
cp .env.example .env          # sửa FUNDUS_ROOT trỏ đúng thư mục ảnh backend
uvicorn app:app --host 0.0.0.0 --port 8000
```
venv\Scripts\python.exe -m uvicorn app:app --host 0.0.0.0 --port 8000

Kiểm tra sống: `curl http://localhost:8000/health` → `{"status":"ok"}`.

> Chưa có weight thì gọi /infer/* sẽ trả 500 "Model weights not found" — đúng
> thiết kế. Đặt weight vào `models/model_N/weights/` là chạy được ngay, không
> cần sửa code.

---

## PHẦN 2 — Backend C# (chỉ 2 chỗ)

### 2.1 THAY FILE `Services/AiInferenceClient.cs`

Chép đè file `AiInferenceClient.cs` (mình gửi kèm) vào:

```
Backend/DiaCompanion/Services/AiInferenceClient.cs
```

Thay đổi so với bản cũ:
- `RunAsync` gọi **3 endpoint** `/infer/dr`, `/infer/lesion`, `/infer/fractal`
  (song song) rồi gộp — thay cho 1 endpoint `/infer` cũ.
- **Bỏ stub** (theo yêu cầu). Constructor bỏ `IConfiguration`.
- Giữ NGUYÊN class `AiInferenceResponse` và interface `IAiInferenceClient`, nên
  `DiagnosesService` và mọi nơi khác KHÔNG phải sửa.

Không cần sửa `Program.cs`: đăng ký `AddHttpClient<IAiInferenceClient,
AiInferenceClient>` dùng DI factory, tự khớp constructor mới. `ImplicitUsings`
đã bật nên không thiếu `using`.

### 2.2 SỬA `appsettings.json` — tắt stub

Tại khối `AiService`, đổi `UseStub` thành `false`:

```json
"AiService": {
  "BaseUrl": "http://localhost:8000",
  "TimeoutSeconds": 60,
  "UseStub": false
}
```

> `UseStub` giờ không còn được đọc trong code (đã bỏ stub), nhưng để `false` cho
> khỏi hiểu nhầm. `BaseUrl` phải trỏ đúng dịch vụ Python.

Không cần migration DB, không cần đổi entity — mọi cột (`DrGrade`, `CountMA`,
`FractalDimension`, `LesionMaskPath`…) đã có sẵn.

---

## PHẦN 3 — Khớp đường dẫn ảnh (quan trọng)

Backend lưu ảnh ở `Storage:FundusRoot` (mặc định `../../storage/fundus`) và gửi
cho AI **đường dẫn tương đối** (`image.FilePath`). Dịch vụ Python phải đọc được
đúng file đó, nên `FUNDUS_ROOT` trong `.env` của `ai_service` phải trỏ tới **cùng
thư mục vật lý**.

Ví dụ nếu cấu trúc là:
```
<repo>/storage/fundus/...        ← ảnh thật
<repo>/Backend/DiaCompanion/     ← backend chạy ở đây, FundusRoot=../../storage/fundus
<repo>/ai_service/               ← .env: FUNDUS_ROOT=../storage/fundus
```

Kiểm nhanh: lấy một `FilePath` trong bảng `FundusImages`, ghép với `FUNDUS_ROOT`,
xem file có tồn tại không.

---

## Thứ tự khởi động khi test

1. Chạy `ai_service` (uvicorn) trước, đợi log "Application startup complete".
2. Chạy backend (`dotnet run`).
3. Đăng nhập bác sĩ → mở một ảnh đã đạt chất lượng → bấm "Chạy AI".
4. Lần đầu chậm (Python nạp weight); các lần sau nhanh.

---

## Những điểm CHƯA làm (không nằm trong phạm vi lần này)

Ghi lại để bạn chủ động, không phải lỗi:

1. **Hiển thị ảnh mask ở frontend.** Dịch vụ Python ghi lesion/vessel mask ra
   `MASK_OUTPUT_ROOT` và backend lưu path vào cột `LesionMaskPath`/`VesselMaskPath`.
   Nhưng `AiDiagnosisDto` hiện KHÔNG trả các path này ra FE, và backend chưa có
   endpoint phục vụ ảnh mask. Muốn hiện mask trên FE cần: thêm field vào
   `AiDiagnosisDto` + một endpoint serve file mask (giống cách phục vụ ảnh fundus
   qua kiểm JWT). Nói mình nếu muốn làm bước này.

2. **confidence & lesion_grade là heuristic tạm.** Xem ghi chú trong
   `model_runners.py`:
   - Model 1 là hồi quy, chưa có softmax → confidence suy từ khoảng cách tới
     ngưỡng. Backend dùng confidence để quyết định deferral, nên khi có model
     phân loại thật hãy thay bằng max-probability để ngưỡng tin cậy đúng nghĩa.
   - `lesion_grade` suy theo luật số lượng tổn thương đơn giản; backend dùng nó
     so với dr_grade để tính "bất đồng". Chỉnh luật nếu cần chuẩn lâm sàng.

3. **model_path chưa dùng.** Backend gửi `model.FilePath` (ModelVersion active),
   nhưng dịch vụ Python dùng weight đóng gói sẵn trong `models/model_N/weights/`
   nên bỏ qua tham số này. Nếu sau muốn quản lý version model qua DB thì map
   `model_path` sang thư mục weight tương ứng.

4. **Chạy nhiều instance.** Uvicorn một tiến trình là đủ cho demo. Nếu cần tải
   cao thì thêm workers, nhưng mỗi worker nạp weight riêng (tốn RAM/VRAM).
