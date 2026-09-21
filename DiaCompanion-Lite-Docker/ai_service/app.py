"""
app.py — Dịch vụ suy luận AI cho DiaCompanion (FastAPI)
=======================================================
Ba endpoint riêng, backend C# gọi lần lượt rồi tự gộp:

  POST /infer/dr        body {"image_path": "..."}  -> dr_grade, confidence, probabilities
  POST /infer/lesion    body {"image_path": "..."}  -> lesion_grade, count_*, area_*, lesion_mask_path
  POST /infer/fractal   body {"image_path": "..."}  -> fractal_dimension, fractal_note, vessel_mask_path

  GET  /health          -> {"status":"ok"}  (kiểm tra dịch vụ sống)

Chạy:
    uvicorn app:app --host 0.0.0.0 --port 8000

Model chỉ nạp một lần rồi giữ nóng (nhờ lru_cache trong model_runners), nên lần
gọi đầu chậm (nạp weight), các lần sau nhanh.

NGUYÊN TẮC (khớp backend NT-3): dịch vụ này CHỈ trả dự đoán, KHÔNG kết luận.
FinalGrade do bác sĩ quyết định ở bước duyệt/ghi đè.
"""

import time
import logging

from fastapi import FastAPI
from pydantic import BaseModel

import model_runners as mr

logging.basicConfig(level=logging.INFO)
log = logging.getLogger("ai_service")

app = FastAPI(title="DiaCompanion AI Inference", version="0.1.0")


class InferRequest(BaseModel):
    image_path: str
    # backend cũng gửi model_path (đường dẫn ModelVersion đang active). Bản thử
    # nghiệm dùng weight đóng gói sẵn trong models/model_N/weights nên bỏ qua,
    # nhưng vẫn khai báo để không lỗi khi backend gửi kèm.
    model_path: str | None = None
    # Chỉ Module 3 dùng: "OD" (mắt phải) | "OS" (mắt trái).
    # Trục nasal-temporal đảo chiều giữa hai mắt, nên không có giá trị này thì
    # các chỉ số fractal theo vùng bị bỏ qua thay vì trả số sai lệch.
    eye: str | None = None


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/infer/dr")
def infer_dr(req: InferRequest):
    t0 = time.time()
    result = mr.run_dr(req.image_path)
    result["inference_ms"] = int((time.time() - t0) * 1000)
    log.info("DR %s -> grade=%s conf=%s", req.image_path, result["dr_grade"], result["confidence"])
    return result


@app.post("/infer/lesion")
def infer_lesion(req: InferRequest):
    t0 = time.time()
    result = mr.run_lesion(req.image_path)
    result["inference_ms"] = int((time.time() - t0) * 1000)
    log.info("Lesion %s -> grade=%s", req.image_path, result["lesion_grade"])
    return result


@app.post("/infer/fractal")
def infer_fractal(req: InferRequest):
    t0 = time.time()
    result = mr.run_fractal(req.image_path, req.eye)
    result["inference_ms"] = int((time.time() - t0) * 1000)
    log.info(
        "Fractal %s eye=%s -> FD=%s asym=%s",
        req.image_path, req.eye,
        result["fractal_dimension"], result.get("fractal_asymmetry"),
    )
    return result
