# DiaCompanion — AI Inference Service

Dịch vụ Python (FastAPI) chạy 3 model và trả kết quả cho backend C#.

## Cấu trúc

```
ai_service/
├── app.py              # FastAPI: 3 endpoint /infer/dr, /infer/lesion, /infer/fractal
├── model_runners.py    # nạp 3 predict.py + dịch output sang snake_case backend cần
├── requirements.txt
├── .env.example
└── models/
    ├── model_1/        # DR grading (EfficientNet-B4)
    │   ├── predict.py
    │   └── weights/    # đặt file .pth vào đây (nạp sau)
    ├── model_2/        # Lesion segmentation (U-Net)
    │   ├── predict.py
    │   └── weights/    # tjdr_unet_v4_best.pth
    └── model_3/        # Fractal / vessel (Keras)
        ├── predict.py
        └── weights/    # best_model.keras
```

## Cài đặt

```bash
cd ai_service
python -m venv venv
source venv/bin/activate        # Windows: venv\Scripts\activate
pip install -r requirements.txt
cp .env.example .env             # sửa FUNDUS_ROOT trỏ đúng thư mục ảnh của backend
```

## Đặt weight (nạp sau)

Copy các file trọng số vào đúng `models/model_N/weights/`:

- model_1: `efficientnet_b4_fold0_best.pth` … `fold4_best.pth` (1–5 fold đều được)
- model_2: `tjdr_unet_v4_best.pth`
- model_3: `best_model.keras`

Chưa có weight thì endpoint sẽ trả lỗi 500 với thông báo "Model weights not
found" — đúng như thiết kế (backend không tạo bản ghi khi AI lỗi).

## Chạy

```bash
uvicorn app:app --host 0.0.0.0 --port 8000
```

Kiểm tra: `curl http://localhost:8000/health` → `{"status":"ok"}`.

## Kết nối với backend

Trong `appsettings.json` của backend, đặt:

```json
"AiService": {
  "BaseUrl": "http://localhost:8000",
  "TimeoutSeconds": 60,
  "UseStub": false
}
```

`UseStub: false` để backend gọi dịch vụ thật thay vì dữ liệu giả.

## Hợp đồng dữ liệu

Mỗi endpoint nhận `{"image_path": "<đường dẫn tương đối>", "model_path": "..."}`
và trả JSON snake_case đúng field backend đọc (xem `model_runners.py`). Backend
gọi cả 3 rồi gộp thành một bản ghi AiDiagnosis.

## Ghi chú kỹ thuật (bản thử nghiệm)

- **confidence** của model 1: hiện suy từ khoảng cách rawScore tới ranh giới
  ngưỡng (model là hồi quy, chưa có softmax). Khi có model phân loại thật, thay
  bằng max-probability.
- **lesion_grade**: suy theo heuristic số lượng tổn thương (trong
  `model_runners._imply_grade_from_lesions`). Backend dùng nó để tính "bất đồng"
  với dr_grade. Chỉnh luật này nếu cần độ chính xác lâm sàng.
- Model nạp một lần rồi giữ nóng; lần gọi đầu chậm do nạp weight.
