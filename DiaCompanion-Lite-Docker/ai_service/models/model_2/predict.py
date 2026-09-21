"""
Module 2 - Lesion Segmentation (U-Net with EfficientNet-B2 encoder, PyTorch,
trained on TJDR (gộp 561 ảnh, split 70/15/15 seed=42), 5-class:
background/EX/HE/MA/SE).

Single-image inference. Outputs JSON to stdout, saves a colorized lesion
mask and an annotated bounding-box overlay to --output-dir, and returns
their file PATHS (not base64) alongside per-lesion pixel counts and
connected-component counts.

Usage:
    python predict.py <image_path> --output-dir <dir>
"""
import os
import sys
import json
import argparse
import cv2
import numpy as np
import torch
import segmentation_models_pytorch as smp
import albumentations as A
from albumentations.pytorch import ToTensorV2

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
WEIGHTS_DIR = os.path.join(SCRIPT_DIR, "weights")
MODEL_PATH = os.path.join(WEIGHTS_DIR, "tjdr_unet_v4_2_best.pth")

# Must match training exactly (see notebook tjdr_unet_v4_2.ipynb).
ENCODER_NAME = "efficientnet-b2"
SIZE = 768
MEAN = (0.485, 0.456, 0.406)
STD = (0.229, 0.224, 0.225)
NUM_CLASSES = 5  # 0=background, 1=EX, 2=HE, 3=MA, 4=SE

LESION_NAMES = {1: "EX", 2: "HE", 3: "MA", 4: "SE"}

# Colors matched to the OFFICIAL legend in Figure 6 of the original TJDR
# paper (Mao et al.): BG=black, EX=red, HE=green, MA=olive/dark-yellow,
# SE=navy. Verified directly against the paper's published figure legend
# (not assumed from a generic RGB convention).
LESION_COLORS_BGR = {
    1: (0, 0, 255),      # EX -> red       RGB(255,0,0)
    2: (0, 255, 0),      # HE -> green     RGB(0,255,0)
    3: (0, 128, 128),    # MA -> olive     RGB(128,128,0)
    4: (128, 0, 0),      # SE -> navy      RGB(0,0,128)
}

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

inference_tf = A.Compose([
    A.Resize(SIZE, SIZE),
    A.Normalize(mean=MEAN, std=STD),
    ToTensorV2()
])

def _resolve_weights(weights_path):
    """weights_path đến từ ModelVersion.FilePath do Admin đăng ký. Không truyền
    hoặc đường dẫn không tồn tại thì dùng MODEL_PATH mặc định."""
    if weights_path:
        p = weights_path if os.path.isabs(weights_path) else os.path.join(SCRIPT_DIR, "..", "..", weights_path)
        p = os.path.normpath(p)
        if os.path.isfile(p):
            return p
    return MODEL_PATH


# Cache theo đường dẫn, không phải một biến toàn cục duy nhất: đổi phiên bản
# model ở màn Admin phải nạp trọng số mới chứ không dùng lại bản cũ.
_MODEL_CACHE = {}


def load_model(weights_path=None):
    target = _resolve_weights(weights_path)
    if target in _MODEL_CACHE:
        return _MODEL_CACHE[target], target
    if not os.path.exists(target):
        raise RuntimeError(f"Model weights not found at {target}")
    model = smp.Unet(
        encoder_name=ENCODER_NAME, encoder_weights=None,
        in_channels=3, classes=NUM_CLASSES
    )
    model.load_state_dict(torch.load(target, map_location=device))
    model.to(device)
    model.eval()
    _MODEL_CACHE[target] = model
    return model, target


def count_lesion_regions(binary_mask):
    num_labels, _ = cv2.connectedComponents(binary_mask.astype(np.uint8))
    return max(0, num_labels - 1)


def get_bounding_boxes(binary_mask, min_area=4):
    num_labels, labels = cv2.connectedComponents(binary_mask.astype(np.uint8))
    boxes = []
    for label_id in range(1, num_labels):
        component_mask = (labels == label_id).astype(np.uint8)
        if component_mask.sum() < min_area:
            continue
        x, y, w, h = cv2.boundingRect(component_mask)
        boxes.append((x, y, w, h))
    return boxes


def draw_annotated_overlay(original_bgr, pred_mask, original_w, original_h):
    scale_x = original_w / SIZE
    scale_y = original_h / SIZE
    overlay = original_bgr.copy()
    thickness = max(2, int(round(min(original_w, original_h) / 400)))

    for cls, color in LESION_COLORS_BGR.items():
        binary = (pred_mask == cls).astype(np.uint8)
        for (x, y, w, h) in get_bounding_boxes(binary):
            x0 = int(round(x * scale_x)); y0 = int(round(y * scale_y))
            x1 = int(round((x + w) * scale_x)); y1 = int(round((y + h) * scale_y))
            cv2.rectangle(overlay, (x0, y0), (x1, y1), color, thickness)

    legend_height = max(50, int(round(original_h * 0.07)))
    legend = np.full((legend_height, original_w, 3), 255, dtype=np.uint8)
    swatch_size = int(legend_height * 0.45)
    font_scale = max(0.5, original_w / 1400)
    font_thickness = max(1, int(round(font_scale * 2)))
    gap = int(legend_height * 0.25)
    x_cursor = gap

    for cls, name in LESION_NAMES.items():
        color = LESION_COLORS_BGR[cls]
        y0 = (legend_height - swatch_size) // 2
        cv2.rectangle(legend, (x_cursor, y0), (x_cursor + swatch_size, y0 + swatch_size), color, -1)
        x_cursor += swatch_size + gap // 2
        text_size, _ = cv2.getTextSize(name, cv2.FONT_HERSHEY_SIMPLEX, font_scale, font_thickness)
        text_y = (legend_height + text_size[1]) // 2
        cv2.putText(legend, name, (x_cursor, text_y), cv2.FONT_HERSHEY_SIMPLEX,
                    font_scale, (0, 0, 0), font_thickness, cv2.LINE_AA)
        x_cursor += text_size[0] + gap * 2

    return np.vstack([legend, overlay])


def predict(image_path, output_dir, weights_path=None):
    if not os.path.exists(image_path):
        raise FileNotFoundError(f"Image not found: {image_path}")
    os.makedirs(output_dir, exist_ok=True)

    model, weights_used = load_model(weights_path)

    image_bgr = cv2.imread(image_path)
    if image_bgr is None:
        raise ValueError("Could not decode image file")

    original_h, original_w = image_bgr.shape[:2]
    image_rgb = cv2.cvtColor(image_bgr, cv2.COLOR_BGR2RGB)

    transformed = inference_tf(image=image_rgb)
    x = transformed["image"].unsqueeze(0).to(device)

    with torch.no_grad():
        logits = model(x)
        pred_mask = logits.argmax(dim=1).squeeze(0).cpu().numpy().astype(np.uint8)

    lesion_counts, pixel_counts = {}, {}
    for cls, name in LESION_NAMES.items():
        binary = (pred_mask == cls).astype(np.uint8)
        lesion_counts[name] = count_lesion_regions(binary)
        pixel_counts[name] = int(binary.sum())

    color_mask = np.zeros((SIZE, SIZE, 3), dtype=np.uint8)
    for cls, color in LESION_COLORS_BGR.items():
        color_mask[pred_mask == cls] = color
    color_mask_resized = cv2.resize(color_mask, (original_w, original_h), interpolation=cv2.INTER_NEAREST)

    mask_path = os.path.join(output_dir, "mask.png")
    cv2.imwrite(mask_path, color_mask_resized)

    annotated_overlay = draw_annotated_overlay(image_bgr, pred_mask, original_w, original_h)
    annotated_path = os.path.join(output_dir, "annotated.png")
    cv2.imwrite(annotated_path, annotated_overlay)

    return {
        "lesionCounts": lesion_counts,
        "pixelCounts": pixel_counts,
        "maskPath": mask_path,
        "annotatedPath": annotated_path,
        "weightsUsed": weights_used
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("image_path")
    parser.add_argument("--output-dir", required=True)
    args = parser.parse_args()

    try:
        result = predict(args.image_path, args.output_dir)
        print(json.dumps({"module": "module2", "status": "success", "result": result}))
    except Exception as e:
        print(json.dumps({"module": "module2", "status": "error", "message": str(e)}))
        sys.exit(1)
