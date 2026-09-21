# DiaCompanion — chạy 3 AI model độc lập

## ModelType

- `1` = DR grading (`POST /infer/dr`)
- `2` = Lesion segmentation (`POST /infer/lesion`)
- `3` = Fractal/vessel (`POST /infer/fractal`)

Mỗi loại có tối đa **1 ModelVersion active**. Vì vậy hệ thống có thể có đồng thời đúng 3 model active, một model cho mỗi nhánh.

## Luồng chạy

`POST /api/diagnoses/run/{imageId}`:

1. Kiểm tra ảnh Gradable và bác sĩ phụ trách.
2. Đọc toàn bộ ModelVersion đang active.
3. Bắt buộc có đủ DR + Lesion + Fractal.
4. Gọi song song 3 endpoint Python, mỗi endpoint nhận `model_path` riêng.
5. Gộp output để tính disagreement/deferral.
6. Lưu 1 `AiDiagnosis` tổng hợp nhưng giữ đủ version đã tạo ra từng phần:
   - `ModelVersionId`: DR model (giữ tên cũ để tương thích)
   - `LesionModelVersionId`
   - `FractalModelVersionId`

## Đăng ký model

`POST /api/admin/models`

Ví dụ DR:

```json
{
  "modelType": 1,
  "name": "dr-efficientnet-b4-v3",
  "filePath": "models/dr/dr-efficientnet-b4-v3.keras",
  "sha256": "<64 hex chars>",
  "qwk": 0.85,
  "note": "DR grading production model"
}
```

Lesion dùng `modelType: 2`; Fractal dùng `modelType: 3`.

Sau khi đăng ký, gọi `PUT /api/admin/models/{id}/activate` cho từng loại. Kích hoạt model mới chỉ tắt model active **cùng ModelType**, không ảnh hưởng hai loại còn lại.

## Database

Chạy script:

`Database/20260811_ThreeAiModels.sql`

trước khi chạy backend mới.
