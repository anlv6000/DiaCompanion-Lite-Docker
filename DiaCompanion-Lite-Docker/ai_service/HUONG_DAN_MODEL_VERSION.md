# Kích hoạt thật sự chức năng đăng ký Model

## Vì sao trước đây không có tác dụng

Chuỗi truyền dữ liệu bị đứt ở mắt xích cuối:

```
Admin đăng ký ModelVersion.FilePath        (Model Governance)
        ↓  lưu vào DB                       ✔
DiagnosesService lấy model đang active      ✔  GetActiveModelVersionsAsync
        ↓  truyền drModel.FilePath
AiInferenceClient gửi model_path            ✔  POST /infer/* { model_path: ... }
        ↓
app.py nhận req.model_path                  ✔  có trong InferRequest
        ↓
model_runners.run_dr(image_path)            ✘  KHÔNG nhận model_path
        ↓
predict.py                                  ✘  nạp trọng số HARDCODE
```

Ba file `predict.py` đều nạp trọng số từ đường dẫn cố định:

| Module | Đường dẫn hardcode |
|---|---|
| model_1 | `models/model_1/weights/efficientnet_b4_fold{0..4}_best.pth` |
| model_2 | `models/model_2/weights/tjdr_unet_v4_2_best.pth` |
| model_3 | `models/model_3/weights/best_model.keras` |

Hệ quả: Admin đăng ký bao nhiêu phiên bản, kích hoạt phiên bản nào cũng không thay đổi gì — hệ thống luôn chạy đúng một bộ trọng số nằm trên đĩa. Bản ghi `ModelVersionId` lưu trong `AiDiagnoses` vì thế **không phản ánh mô hình thật sự đã chạy**, mà chỉ ghi lại phiên bản nào đang được đánh dấu active.

Đây là điểm hội đồng dễ hỏi, vì BR-17 trong SRS phát biểu *"Each AI result shall retain the model-version identifier that produced it"* — trước bản vá này thì phát biểu đó chưa đúng.

## Sau bản vá

`model_path` được truyền xuyên suốt tới `predict.py`. Mỗi module giải đường dẫn theo quy tắc:

1. Đường dẫn tuyệt đối và tồn tại → dùng luôn
2. Đường dẫn tương đối → ghép với thư mục `ai_service/`
3. Không truyền, hoặc đường dẫn không tồn tại → **quay về trọng số mặc định**

Quy tắc 3 quan trọng: hệ thống không bao giờ chết vì cấu hình sai, chỉ âm thầm dùng bản mặc định. Để biết bản nào thật sự chạy, phản hồi của cả ba endpoint giờ có thêm trường `weights_used`, và log uvicorn cũng in ra.

Tiện thể sửa một vấn đề hiệu năng: model_1 trước đây nạp lại **toàn bộ 5 fold ở mỗi lần suy luận**. Giờ có cache theo đường dẫn — đổi phiên bản ở màn Admin vẫn nạp trọng số mới, nhưng chạy cùng một phiên bản nhiều lần thì chỉ nạp một lần.

---

## Năm file cần thay

| File | Đường dẫn đích |
|---|---|
| `app.py` | `ai_service/app.py` |
| `model_runners.py` | `ai_service/model_runners.py` |
| `predict_model_1.py` | `ai_service/models/model_1/predict.py` |
| `predict_model_2.py` | `ai_service/models/model_2/predict.py` |
| `predict_model_3.py` | `ai_service/models/model_3/predict.py` |

Không cần sửa backend: `AiInferenceClient` đã gửi `model_path` từ trước.

Khởi động lại uvicorn sau khi thay.

---

## Cách đăng ký một phiên bản

### Bước 1 — Đặt tệp trọng số vào đúng chỗ

Quy ước đề xuất, để đường dẫn trong DB ngắn và không phụ thuộc ổ đĩa:

```
ai_service/
  models/
    model_1/weights/v4.0/efficientnet_b4_fold0_best.pth … fold4_best.pth
    model_2/weights/tjdr_unet_v4_2_best.pth
    model_3/weights/best_model.keras
```

### Bước 2 — Tính SHA-256

Trường `Sha256` là bắt buộc và phải đúng 64 ký tự hệ 16.

```cmd
certutil -hashfile "models\model_2\weights\tjdr_unet_v4_2_best.pth" SHA256
```

Với model_1 là ensemble nhiều tệp, hãy lấy SHA-256 của **fold 0** và ghi rõ trong `Note` rằng đó là đại diện — hoặc nén cả thư mục thành một tệp rồi băm tệp đó.

### Bước 3 — Đăng ký ở màn Model Governance

| Trường | DR | Lesion | Fractal |
|---|---|---|---|
| ModelType | 1 | 2 | 3 |
| Name | `efficientnet-b4-v4.0` | `tjdr-unet-v4.2` | `fives-unet-v1.0` |
| FilePath | `models/model_1/weights/v4.0` | `models/model_2/weights/tjdr_unet_v4_2_best.pth` | `models/model_3/weights/best_model.keras` |
| Sha256 | 64 ký tự hex | 64 ký tự hex | 64 ký tự hex |
| Chỉ số | **QWK bắt buộc** | Dice hoặc IoU | Dice hoặc IoU |

Lưu ý về `FilePath` của DR: model_1 là ensemble nên **trỏ vào THƯ MỤC** chứa các tệp `efficientnet_b4_fold*.pth`. Nếu trỏ vào một tệp `.pth` đơn lẻ thì ensemble chỉ có một thành viên — vẫn chạy, nhưng kết quả khác với bản 5 fold.

Ràng buộc backend đang áp:
- `Name` phải là duy nhất
- Phải có ít nhất một trong ba chỉ số QWK / Dice / IoU
- Mọi chỉ số phải nằm trong đoạn 0–1

### Bước 4 — Kích hoạt

Mỗi `ModelType` chỉ có **một** phiên bản active tại một thời điểm, được bảo đảm bằng unique filtered index `UX_ModelVersions_ActivePerType`. Kích hoạt bản mới sẽ tự tắt bản cũ cùng loại trong cùng một transaction.

Điều kiện kích hoạt do `ValidateActivationMetrics` kiểm:
- DR: bắt buộc có QWK
- Lesion và Fractal: bắt buộc có Dice hoặc IoU

Kết quả đã lưu **không** bị ảnh hưởng — mỗi `AiDiagnoses` giữ `ModelVersionId` của lần chạy, đúng tinh thần BR-17.

### Bước 5 — Xác minh model mới thật sự được dùng

Đây là bước trước đây không làm được.

**Cách 1 — log uvicorn.** Chạy AI một ảnh rồi xem cửa sổ uvicorn:

```
DR fundus/2026/BN.../v1033_xxx.jpg -> grade=2 conf=0.73 weights=...\models\model_1\weights\v4.0
```

Đường dẫn in ra phải khớp `FilePath` của phiên bản vừa kích hoạt.

**Cách 2 — gọi thẳng endpoint.** Trong trình duyệt mở `http://127.0.0.1:8000/docs`, gọi `/infer/dr` với `model_path` khác nhau và so kết quả.

**Cách 3 — phép thử quyết định.** Đăng ký một phiên bản trỏ vào đường dẫn **cố tình sai**, kích hoạt, rồi chạy AI. Nếu log vẫn in ra đường dẫn trọng số mặc định thì chuỗi truyền đang đứt; nếu in ra đường dẫn mặc định *và* bạn đã thay đủ năm file thì kiểm lại đã khởi động lại uvicorn chưa.

---

## Ba việc nên làm tiếp

**Kiểm SHA-256 lúc nạp.** Trường `Sha256` hiện chỉ được kiểm định dạng khi đăng ký, chưa bao giờ được dùng để đối chiếu tệp thật. Comment trong entity ghi *"QT-20: verify lúc nạp. Trả lời được 'làm sao biết file model không bị tráo?'"* — nhưng chưa có dòng code nào làm việc đó. Muốn hoàn chỉnh thì backend cần gửi thêm `model_sha256`, ai_service băm tệp lúc nạp và từ chối nếu lệch. Việc này biến `Sha256` từ một trường trang trí thành một cơ chế kiểm soát thật.

**Ghi `weights_used` vào bản ghi chẩn đoán.** Hiện giá trị này chỉ có trong log và trong phản hồi HTTP. Lưu vào `AiDiagnoses` sẽ cho phép truy vết về sau mà không cần đọc log.

**Cảnh báo khi rơi về trọng số mặc định.** Hiện tại nếu `FilePath` sai thì hệ thống im lặng dùng bản mặc định. An toàn cho vận hành, nhưng nên hiện cảnh báo ở màn Model Governance để Admin biết cấu hình chưa đúng.
