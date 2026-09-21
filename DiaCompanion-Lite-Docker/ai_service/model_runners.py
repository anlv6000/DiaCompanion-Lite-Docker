"""
model_runners.py
================
Lớp trung gian giữa 3 script predict.py (mỗi model một format riêng) và HỢP ĐỒNG
JSON mà backend C# (AiInferenceClient.AiInferenceResponse) mong đợi.

Backend gọi 3 endpoint riêng (/infer/dr, /infer/lesion, /infer/fractal) và tự
gộp lại. Mỗi hàm dưới đây trả về đúng các field snake_case backend đọc được:

  DR      -> dr_grade, confidence, probabilities
  Lesion  -> lesion_grade, count_ma/he/ex/se, area_ma/he/ex/se, lesion_mask_path
  Fractal -> fractal_dimension, fractal_note, vessel_mask_path

Ảnh mask được ghi ra thư mục MASK_OUTPUT_DIR và trả về ĐƯỜNG DẪN TƯƠNG ĐỐI
(backend lưu path vào cột *_mask_path, không lưu base64).

predict.py của từng model GIỮ NGUYÊN, không sửa. Ở đây chỉ nạp động rồi dịch.
"""

import os
import base64
import importlib.util
import sys
import threading
import inspect
import uuid
from functools import lru_cache

# --- Cấu hình đường dẫn ------------------------------------------------------
SERVICE_DIR = os.path.dirname(os.path.abspath(__file__))
MODELS_DIR = os.path.join(SERVICE_DIR, "models")

# Thư mục gốc chứa ảnh fundus (khớp Storage:FundusRoot của backend). Backend gửi
# đường dẫn tương đối, ta ghép với thư mục gốc này để ra file thật.
FUNDUS_ROOT = os.environ.get(
    "FUNDUS_ROOT",
    os.path.join(SERVICE_DIR, "..", "storage"),
)

# Nơi lưu ảnh mask sinh ra. Nên trỏ vào cùng khu vực backend có thể phục vụ lại
# (ví dụ một thư mục con trong storage). Trả path tương đối tính từ thư mục này.
MASK_OUTPUT_ROOT = os.environ.get(
    "MASK_OUTPUT_ROOT",
    os.path.join(SERVICE_DIR, "..", "storage", "ai_masks"),
)

# Kích thước lưới model 2 (lesion) để quy đổi pixel -> tỉ lệ diện tích.
LESION_GRID = 768 * 768

# Serialize first-time dynamic imports. The three FastAPI endpoints may run in
# separate worker threads; importing PyTorch/torchvision-related modules at the
# same time can expose a partially initialized torchvision.transforms module.
_MODEL_IMPORT_LOCK = threading.RLock()


def resolve_image(image_path: str) -> str:
    """Backend gửi path tương đối -> ghép với FUNDUS_ROOT. Nếu đã là path tuyệt
    đối và tồn tại thì dùng luôn."""
    if os.path.isabs(image_path) and os.path.exists(image_path):
        return image_path
    return os.path.normpath(os.path.join(FUNDUS_ROOT, image_path))


@lru_cache(maxsize=8)
def _load_predict(model_number: int):
    """Load predict() once, while serializing heavy framework imports."""
    script = os.path.join(MODELS_DIR, f"model_{model_number}", "predict.py")
    if not os.path.exists(script):
        raise RuntimeError(f"Không tìm thấy predict.py cho model {model_number}: {script}")

    with _MODEL_IMPORT_LOCK:
        module_name = f"ai_service_model_{model_number}_predict"
        spec = importlib.util.spec_from_file_location(module_name, script)
        if spec is None or spec.loader is None:
            raise ImportError(f"Không thể tạo import spec cho: {script}")

        module = importlib.util.module_from_spec(spec)
        # Register before executing so imports see a fully tracked module.
        sys.modules[module_name] = module
        try:
            spec.loader.exec_module(module)
        except Exception:
            sys.modules.pop(module_name, None)
            raise

        if not hasattr(module, "predict"):
            sys.modules.pop(module_name, None)
            raise RuntimeError(f"model_{model_number}/predict.py thiếu hàm predict()")
        return module.predict


def _save_mask_from_base64(data_url: str, filename: str) -> str:
    """Nhận chuỗi 'data:image/png;base64,...' -> ghi file PNG vào MASK_OUTPUT_ROOT.
    Trả về đường dẫn TƯƠNG ĐỐI (tính từ MASK_OUTPUT_ROOT) để backend lưu vào DB."""
    if not data_url:
        return None
    b64 = data_url.split(",", 1)[1] if "," in data_url else data_url
    os.makedirs(MASK_OUTPUT_ROOT, exist_ok=True)
    out_path = os.path.join(MASK_OUTPUT_ROOT, filename)
    with open(out_path, "wb") as f:
        f.write(base64.b64decode(b64))
    return os.path.relpath(out_path, MASK_OUTPUT_ROOT).replace("\\", "/")


# ============================================================================
#  MODEL 1 — DR grading
# ============================================================================
def run_dr(image_path: str) -> dict:
    """Trả: dr_grade (0-4), confidence (0-1), probabilities (list|None)."""
    predict = _load_predict(1)
    real_path = resolve_image(image_path)
    raw = predict(real_path)  # {grade:"R2", gradeLabel, rawScore, modelsUsed}

    grade = int(str(raw["grade"]).lstrip("R"))  # "R2" -> 2

    # predict.py model 1 là hồi quy (rawScore), KHÔNG xuất softmax. Ta suy ra một
    # "độ tin cậy" từ khoảng cách rawScore tới ranh giới ngưỡng gần nhất: càng xa
    # ranh giới càng chắc chắn. Đây là heuristic tạm cho bản thử nghiệm; khi có
    # model phân loại thật (softmax) hãy thay confidence bằng max-proba thực.
    thresholds = [0.481, 1.468, 2.286, 3.469]
    raw_score = float(raw.get("rawScore", grade))
    boundaries = [0.0] + thresholds + [4.0]
    lo = boundaries[grade]
    hi = boundaries[grade + 1] if grade + 1 < len(boundaries) else 4.0
    span = max(hi - lo, 1e-6)
    center = (lo + hi) / 2.0
    # 0.5 ở giữa khoảng, tiến tới 1.0 khi sát tâm lớp; kẹp trong [0.5, 0.99].
    closeness = 1.0 - min(abs(raw_score - center) / (span / 2.0), 1.0)
    confidence = round(0.5 + 0.49 * closeness, 4)

    return {
        "dr_grade": grade,
        "confidence": confidence,
        "probabilities": None,  # chưa có softmax thật
    }


# ============================================================================
#  MODEL 2 — Lesion segmentation
# ============================================================================
# Suy ra "mức DR ngụ ý từ tổn thương" để backend so với dr_grade (tính bất đồng).
# Quy tắc heuristic đơn giản theo mức độ tổn thương; thay bằng luật lâm sàng thật
# nếu cần. Ý tưởng: không tổn thương -> 0; có MA nhẹ -> 1; nhiều MA/HE -> 2-3;
# tổn thương nặng/lan rộng -> 4.
def _imply_grade_from_lesions(counts: dict) -> int:
    # Ngưỡng CALIBRATE trên IDRiD (tối đa hoá QWK vs nhãn thật: 0.59 -> 0.77),
    # KHÔNG đặt bằng tay. Chặn trần grade 3: PDR (4) cần tân mạch, không suy
    # được từ MA/HE/EX/SE (ICDR). Nhánh DR vẫn được phép ra grade 4.
    ma = counts.get("MA", 0)
    he = counts.get("HE", 0)
    ex = counts.get("EX", 0)
    se = counts.get("SE", 0)
    if ma + he + ex + se == 0:
        return 0
    if he >= 15 or ex >= 30 or se >= 3:
        return 3          # severe NPDR
    if he >= 8 or ex >= 5 or ma >= 5:
        return 2          # moderate NPDR
    return 1              # chỉ vài tổn thương nhẹ -> mild NPDR


def run_lesion(image_path: str) -> dict:
    """Trả: lesion_grade, count_ma/he/ex/se, area_ma/he/ex/se, lesion_mask_path."""
    predict = _load_predict(2)
    real_path = resolve_image(image_path)
    output_dir = os.path.join(MASK_OUTPUT_ROOT, "lesion", uuid.uuid4().hex)
    raw = predict(real_path, output_dir)
    # raw: {lesionCounts, pixelCounts, maskPath, annotatedPath}

    counts = raw.get("lesionCounts", {})
    pixels = raw.get("pixelCounts", {})

    def area(name):
        # tỉ lệ diện tích tổn thương trên toàn ảnh lưới 768x768
        px = pixels.get(name, 0)
        return round(px / LESION_GRID, 6) if px else 0.0

    mask_path = raw.get("annotatedPath")
    mask_rel = (
        os.path.relpath(mask_path, MASK_OUTPUT_ROOT).replace("\\", "/")
        if mask_path else None
    )

    return {
        "lesion_grade": _imply_grade_from_lesions(counts),
        "count_ma": int(counts.get("MA", 0)),
        "count_he": int(counts.get("HE", 0)),
        "count_ex": int(counts.get("EX", 0)),
        "count_se": int(counts.get("SE", 0)),
        "area_ma": area("MA"),
        "area_he": area("HE"),
        "area_ex": area("EX"),
        "area_se": area("SE"),
        "lesion_mask_path": mask_rel,
    }


# ============================================================================
#  MODEL 3 — Fractal / vessel
# ============================================================================
def run_fractal(image_path: str, eye: str | None = None) -> dict:
    """Trả các chỉ số fractal theo vùng dưới dạng số.

    eye: "OD" (mắt phải) | "OS" (mắt trái) | None.
         None thì chỉ có FD_total; các chỉ số theo vùng trả None vì trục
         nasal-temporal đảo chiều giữa hai mắt, gán nhầm còn tệ hơn bỏ trống.
    """
    predict = _load_predict(3)
    real_path = resolve_image(image_path)
    output_dir = os.path.join(MASK_OUTPUT_ROOT, "fractal", uuid.uuid4().hex)

    # predict.py mới nhận thêm tham số eye. Dùng inspect để vẫn chạy được với
    # bản predict.py cũ (2 tham số) trong lúc chuyển đổi.
    try:
        sig = inspect.signature(predict)
        if len(sig.parameters) >= 3:
            raw = predict(real_path, output_dir, eye)
        else:
            raw = predict(real_path, output_dir)
    except (TypeError, ValueError):
        raw = predict(real_path, output_dir)

    vessel_path = raw.get("vesselMaskPath")
    vessel_rel = (
        os.path.relpath(vessel_path, MASK_OUTPUT_ROOT).replace("\\", "/")
        if vessel_path else None
    )

    fd_total = raw.get("FD_total")
    fd_st = raw.get("FD_ST")
    fd_sn = raw.get("FD_SN")
    fd_it = raw.get("FD_IT")
    fd_in = raw.get("FD_IN")
    fd_asym = raw.get("FD_asym")
    fd_tn = raw.get("FD_TN")
    lac = raw.get("lacunarity")

    # Chú thích ngắn cho bác sĩ đọc. KHÔNG phải nơi lưu dữ liệu — mọi chỉ số
    # đều đã có trường số riêng ở dưới.
    note = None
    if fd_total is not None:
        parts = [f"FD tổng={fd_total}"]
        if fd_asym is not None:
            parts.append(f"bất đối xứng giữa vùng={fd_asym}")
        if fd_tn is not None:
            huong = "thái dương" if fd_tn > 0 else "mũi"
            parts.append(f"lệch về phía {huong}={abs(fd_tn)}")
        if all(v is not None for v in (fd_st, fd_sn, fd_it, fd_in)):
            parts.append(f"ST={fd_st}, SN={fd_sn}, IT={fd_it}, IN={fd_in}")
        note = "; ".join(parts)[:300]  # khớp MaxLength(300) của cột FractalNote

    return {
        "fractal_dimension": fd_total,
        "fractal_st": fd_st,
        "fractal_sn": fd_sn,
        "fractal_it": fd_it,
        "fractal_in": fd_in,
        "fractal_asymmetry": fd_asym,
        "fractal_tn": fd_tn,
        "lacunarity": lac,
        "fractal_note": note,
        "vessel_mask_path": vessel_rel,
    }


