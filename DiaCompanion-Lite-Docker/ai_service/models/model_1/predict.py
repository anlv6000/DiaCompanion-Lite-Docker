"""
Module 1 - Diabetic Retinopathy Grading (EfficientNet-B4, 5-fold ensemble)
Single-image inference. Outputs JSON to stdout for the Node.js backend to parse.

Usage:
    python predict.py <image_path> --output-dir <dir>

(--output-dir is accepted for CLI consistency with the other 4 modules,
even though this module currently writes no output images of its own.)
"""
import os
import sys
import json
import argparse
import cv2
import numpy as np
from PIL import Image
import torch
import torch.nn as nn
from torchvision import transforms, models

# ==============================================================================
# CONFIG
# ==============================================================================
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
WEIGHTS_DIR = os.path.join(SCRIPT_DIR, "weights")
IMG_SIZE = 512
DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")

# Calibrated OptimizedRounder thresholds (from training)
THRESHOLDS = [0.48123852527499983, 1.4676663433240558, 2.285876286742074, 3.4689338833914567]

CLASS_LABELS = {
    0: "R0 - No DR",
    1: "R1 - Mild NPDR",
    2: "R2 - Moderate NPDR",
    3: "R3 - Severe NPDR",
    4: "R4 - Proliferative DR"
}

transform = transforms.Compose([
    transforms.ToTensor(),
    transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225])
])


def apply_ben_graham(path, sigma=10, size=IMG_SIZE):
    """Ben Graham preprocessing: 4*I - 4*GaussianBlur(I, sigma) + 128, circular crop."""
    stream = open(path, "rb")
    file_bytes = bytearray(stream.read())
    numpy_array = np.asarray(file_bytes, dtype=np.uint8)
    img = cv2.imdecode(numpy_array, cv2.IMREAD_COLOR)

    if img is None:
        return None
    img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    img = cv2.resize(img, (size, size))
    img = cv2.addWeighted(img, 4, cv2.GaussianBlur(img, (0, 0), sigma), -4, 128)
    mask = np.zeros(img.shape, dtype=np.uint8)
    cv2.circle(mask, (size // 2, size // 2), int(size * 0.47), (1, 1, 1), -1)
    return Image.fromarray((img * mask).astype(np.uint8))


def build_model():
    model = models.efficientnet_b4(weights=None)
    in_features = model.classifier[1].in_features
    model.classifier = nn.Sequential(
        nn.Dropout(p=0.3),
        nn.Linear(in_features, 512),
        nn.ReLU(),
        nn.Dropout(p=0.3),
        nn.Linear(512, 1)
    )
    return model


def _resolve_weights(weights_path):
    """Xác định thư mục/tệp trọng số sẽ nạp.

    weights_path đến từ ModelVersion.FilePath mà Admin đăng ký ở màn Model
    Governance. Nếu không truyền hoặc đường dẫn không tồn tại thì quay về
    WEIGHTS_DIR mặc định, để hệ thống vẫn chạy được như trước.

    Model 1 là ensemble 5 fold nên FilePath có thể là:
      - một THƯ MỤC chứa các tệp efficientnet_b4_fold*.pth  (khuyến nghị)
      - một TỆP .pth đơn lẻ, khi đó ensemble chỉ có một thành viên
    """
    if weights_path:
        p = weights_path if os.path.isabs(weights_path) else os.path.join(SCRIPT_DIR, "..", "..", weights_path)
        p = os.path.normpath(p)
        if os.path.exists(p):
            return p
    return WEIGHTS_DIR


# Cache theo đường dẫn trọng số. Trước đây toàn bộ ensemble được nạp lại ở MỖI
# lần suy luận, tốn vài giây cho một thao tác đáng lẽ tức thời.
_ENSEMBLE_CACHE = {}


def load_ensemble(weights_path=None):
    """Load fold checkpoints. Returns (list of eval()-mode models, source path)."""
    target = _resolve_weights(weights_path)
    if target in _ENSEMBLE_CACHE:
        return _ENSEMBLE_CACHE[target]

    models_list = []
    if os.path.isfile(target):
        files = [target]
    elif os.path.isdir(target):
        files = [
            os.path.join(target, f"efficientnet_b4_fold{fold}_best.pth")
            for fold in range(5)
        ]
    else:
        files = []

    for path in files:
        if os.path.exists(path):
            model = build_model()
            checkpoint = torch.load(path, map_location=DEVICE, weights_only=False)
            model.load_state_dict(checkpoint["model_state"])
            model.to(DEVICE)
            model.eval()
            models_list.append(model)

    _ENSEMBLE_CACHE[target] = (models_list, target)
    return models_list, target


def predict(image_path, weights_path=None):
    ensemble_models, weights_used = load_ensemble(weights_path)
    if len(ensemble_models) == 0:
        raise RuntimeError(f"No model weights found in {weights_used}")

    if not os.path.exists(image_path):
        raise FileNotFoundError(f"Image not found: {image_path}")

    pil_img = apply_ben_graham(image_path)
    if pil_img is None:
        raise ValueError("Could not decode image file")

    img_tensor = transform(pil_img).unsqueeze(0).to(DEVICE)
    img_hflip = torch.flip(img_tensor, dims=[3])
    img_vflip = torch.flip(img_tensor, dims=[2])

    raw_score = 0.0
    with torch.no_grad():
        for model in ensemble_models:
            out_orig = model(img_tensor).item()
            out_hflip = model(img_hflip).item()
            out_vflip = model(img_vflip).item()
            raw_score += (out_orig + out_hflip + out_vflip) / 3.0
    raw_score /= len(ensemble_models)

    final_class = 0
    if raw_score >= THRESHOLDS[3]:
        final_class = 4
    elif raw_score >= THRESHOLDS[2]:
        final_class = 3
    elif raw_score >= THRESHOLDS[1]:
        final_class = 2
    elif raw_score >= THRESHOLDS[0]:
        final_class = 1

    return {
        "grade": f"R{final_class}",
        "gradeLabel": CLASS_LABELS[final_class],
        "rawScore": round(raw_score, 4),
        "modelsUsed": len(ensemble_models),
        # Trả về nguồn trọng số thật sự được nạp, để đối chiếu với phiên bản
        # đang active trong màn Model Governance.
        "weightsUsed": weights_used,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("image_path")
    parser.add_argument("--output-dir", required=False, default=None,
                         help="Accepted for CLI consistency; unused by this module.")
    args = parser.parse_args()

    try:
        result = predict(args.image_path)
        print(json.dumps({"module": "module1", "status": "success", "result": result}))
    except Exception as e:
        print(json.dumps({"module": "module1", "status": "error", "message": str(e)}))
        sys.exit(1)
