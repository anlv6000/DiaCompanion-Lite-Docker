"""
Module 3 — Sectorial Fractal Analysis (vessel U-Net + quadrant box-counting)

THAY THẾ hoàn toàn cách tiếp cận FD_thick / FD_thin / delta_FD cũ.

Lý do đổi: hàm classify_vessel_caliber cũ dùng cv2.erode 3x3 một vòng, chỉ bóc
lớp biên 1 pixel của MỌI mạch. Kết quả là "thin" thực chất là ĐƯỜNG VIỀN của
toàn bộ mạch máu chứ không phải mạch mảnh, nên FD_thin đo contour và delta_FD
là hiệu giữa "mạch co lại" với "đường viền mạch" — không phải đại lượng nào có
trong y văn.

Cách mới: chia võng mạc thành 4 góc phần tư giải phẫu, tính FD từng vùng, rồi
đo mức bất đối xứng giữa các vùng.

BỐN QUYẾT ĐỊNH KỸ THUẬT (xem 01_research/00_TONG_QUAN.md muc 3):

  1. FD tính trên MASK, không trên skeleton.
     Đo thực tế trên ảnh mẫu: mask 1.4216 vs skeleton 1.2120.
     Dải chuẩn Liew 2008 là 1.43-1.47, Lyu 2022 báo nhóm ảnh bị loại 1.371.
     Giá trị skeleton thấp hơn cả nhóm ảnh bị loại -> không so được với y văn.
     Skeleton vẫn xuất ra để hiển thị, nhưng không dùng để tính FD.

  2. Lật ngang ảnh MẮT TRÁI (OS) trước khi chia vùng.
     Đĩa thị luôn nằm phía nasal. Ảnh mắt phải: temporal ở nửa trái ảnh;
     mắt trái thì ngược lại. Không lật thì vùng temporal của mắt này bị gộp
     với vùng nasal của mắt kia.

  3. LOẠI vùng đĩa thị trước khi tính FD vùng.
     Đo trên ảnh mẫu, chùm mạch hội tụ ở đĩa thị làm FD vùng chứa nó tăng
     0.043-0.072 — đủ để đảo thứ hạng giữa các vùng.

  4. Dải hộp theo TỈ LỆ VÙNG, hộp lớn nhất <= 1/8 cạnh vùng.
     Toàn ảnh 512: [64,32,16,8,4,2]. Vùng 256: [32,16,8,4,2].
     Đổi dải làm FD lệch tới 0.13 — lớn hơn biên độ khác biệt giữa các vùng.
     FD vùng KHÔNG so được với FD toàn ảnh.

Usage:
    python predict.py <image_path> --output-dir <dir> [--eye OD|OS]
"""
import os
import sys
import json
import argparse

import cv2
import numpy as np
import tensorflow as tf
from skimage.morphology import skeletonize

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
WEIGHTS_DIR = os.path.join(SCRIPT_DIR, "weights")
MODEL_PATH = os.path.join(WEIGHTS_DIR, "best_model.keras")

# Phải khớp input shape của model đã huấn luyện: (None, 512, 512, 3).
# KHÔNG đổi sang 256 — giá trị đó chỉ có trong khối code thăm dò đã comment
# của script gốc và không khớp model thật.
IMG_SIZE = 512

# Bán kính vùng đĩa thị bị loại, tính trên ảnh 512x512 (~1 đường kính đĩa thị).
# Ghi rõ giá trị này trong báo cáo: kết quả phụ thuộc vào nó.
DISC_RADIUS = 60

# Cửa sổ lọc trung bình để dò đĩa thị = nơi mạch máu hội tụ dày nhất.
DISC_PROBE_WINDOW = 60

BOX_SIZES_FULL = [64, 32, 16, 8, 4, 2]   # toàn ảnh 512x512
BOX_SIZES_QUAD = [32, 16, 8, 4, 2]       # mỗi góc phần tư 256x256

# Số thang tối thiểu có hộp chứa mạch thì mới hồi quy. Dưới ngưỡng này trả None
# thay vì để np.polyfit ném lỗi trên mảng rỗng và làm chết cả nhánh fractal.
MIN_VALID_SCALES = 3


# ---------------------------------------------------------------- model
def dice_coef(y_true, y_pred):
    y_true = tf.keras.backend.flatten(y_true)
    y_pred = tf.keras.backend.flatten(y_pred)
    intersection = tf.reduce_sum(y_true * y_pred)
    return (2.0 * intersection + 1) / (tf.reduce_sum(y_true) + tf.reduce_sum(y_pred) + 1)


def dice_loss(y_true, y_pred):
    return 1 - dice_coef(y_true, y_pred)


def _resolve_weights(weights_path):
    """weights_path đến từ ModelVersion.FilePath do Admin đăng ký. Không truyền
    hoặc đường dẫn không tồn tại thì dùng MODEL_PATH mặc định."""
    if weights_path:
        p = weights_path if os.path.isabs(weights_path) else os.path.join(SCRIPT_DIR, "..", "..", weights_path)
        p = os.path.normpath(p)
        if os.path.exists(p):
            return p
    return MODEL_PATH


# Cache theo đường dẫn: đổi phiên bản ở màn Admin thì nạp trọng số mới.
_MODEL_CACHE = {}


def load_model(weights_path=None):
    target = _resolve_weights(weights_path)
    if target in _MODEL_CACHE:
        return _MODEL_CACHE[target], target
    if not os.path.exists(target):
        raise RuntimeError(f"Model weights not found at {target}")
    model = tf.keras.models.load_model(
        target,
        custom_objects={"dice_coef": dice_coef, "dice_loss": dice_loss},
    )
    _MODEL_CACHE[target] = model
    return model, target


# ---------------------------------------------------------------- tiền xử lý
def letterbox(image, size):
    """Co ảnh về size x size nhưng GIỮ TỈ LỆ KHUNG, đệm đen phần thiếu.

    cv2.resize thẳng lên ảnh không vuông sẽ kéo giãn mạch máu theo một chiều,
    làm FD lệch theo tỉ lệ khung của từng máy chụp. Ảnh hiện tại của dự án là
    1024x1024 nên bước này không đổi gì, nhưng cần có để an toàn với máy khác.
    """
    h, w = image.shape[:2]
    scale = size / max(h, w)
    nh, nw = int(round(h * scale)), int(round(w * scale))
    resized = cv2.resize(image, (nw, nh), interpolation=cv2.INTER_AREA)
    canvas = np.zeros((size, size, image.shape[2]), dtype=image.dtype)
    top, left = (size - nh) // 2, (size - nw) // 2
    canvas[top:top + nh, left:left + nw] = resized
    return canvas


def fov_center(image_bgr):
    """Trọng tâm vùng không phải nền đen. Bền hơn giả định tâm hình học vì
    nhiều ảnh bị cắt lệch."""
    gray = cv2.cvtColor(image_bgr, cv2.COLOR_BGR2GRAY)
    fov = gray > 12
    if not fov.any():
        h, w = gray.shape
        return h // 2, w // 2
    ys, xs = np.nonzero(fov)
    return int(round(ys.mean())), int(round(xs.mean()))


def locate_optic_disc(mask):
    """Tâm đĩa thị = cực đại mật độ mạch cục bộ.

    Heuristic, không cần mô hình riêng. Ảnh chất lượng kém hoặc bệnh lý nặng
    có thể làm sai — đã khai báo trong phần giới hạn của nghiên cứu.
    """
    density = cv2.blur(mask.astype(np.float32), (DISC_PROBE_WINDOW, DISC_PROBE_WINDOW))
    cy, cx = np.unravel_index(int(density.argmax()), density.shape)
    return int(cy), int(cx)


def remove_disc(mask, cy, cx, radius=DISC_RADIUS):
    """Đặt về 0 hình tròn quanh đĩa thị."""
    out = mask.copy()
    h, w = out.shape
    yy, xx = np.ogrid[:h, :w]
    out[(yy - cy) ** 2 + (xx - cx) ** 2 <= radius ** 2] = False
    return out


# ---------------------------------------------------------------- fractal
def box_count_fd(binary, sizes):
    """Chiều fractal box-counting.

    FD = hệ số góc của hồi quy log N(eps) theo log(1/eps),
    N(eps) = số ô vuông cạnh eps chứa ít nhất một điểm ảnh mạch máu.

    Trả (fd, counts) hoặc (None, counts) khi dữ liệu quá thưa.
    """
    b = binary > 0
    counts = []
    h, w = b.shape
    for s in sizes:
        c = 0
        for y in range(0, h, s):
            for x in range(0, w, s):
                if b[y:y + s, x:x + s].any():
                    c += 1
        counts.append(c)

    counts_arr = np.array(counts, dtype=float)
    sizes_arr = np.array(sizes, dtype=float)
    valid = counts_arr > 0
    if valid.sum() < MIN_VALID_SCALES:
        return None, counts
    slope = np.polyfit(np.log(1.0 / sizes_arr[valid]), np.log(counts_arr[valid]), 1)[0]
    return float(slope), counts


def lacunarity(binary, box=32):
    """Lacunarity hộp trượt: Lambda = var/mean^2 + 1 trên số điểm ảnh mỗi hộp."""
    b = (binary > 0).astype(np.float32)
    sums = cv2.boxFilter(b, ddepth=-1, ksize=(box, box), normalize=False)
    mean = float(sums.mean())
    if mean <= 0:
        return None
    return float(sums.var() / (mean ** 2) + 1.0)


def quadrant_slices(shape, cy, cx):
    """Bốn góc phần tư quanh tâm FOV.

    Tên vùng theo hệ ĐÃ CHUẨN HOÁ (mắt trái đã được lật):
      nửa trái ảnh  = temporal
      nửa phải ảnh  = nasal
    """
    h, w = shape
    cy = int(np.clip(cy, 1, h - 1))
    cx = int(np.clip(cx, 1, w - 1))
    return {
        "ST": (slice(0, cy), slice(0, cx)),      # superotemporal
        "SN": (slice(0, cy), slice(cx, w)),      # superonasal
        "IT": (slice(cy, h), slice(0, cx)),      # inferotemporal
        "IN": (slice(cy, h), slice(cx, w)),      # inferonasal
    }


# ---------------------------------------------------------------- pipeline
def predict(image_path, output_dir, eye=None, weights_path=None):
    """
    eye: "OD" (mắt phải) | "OS" (mắt trái) | None.

    None nghĩa là không biết mắt nào — khi đó FD_total vẫn tính bình thường,
    nhưng các chỉ số theo vùng bị BỎ QUA (trả None) thay vì trả số sai lệch.
    Trục nasal-temporal đảo chiều giữa hai mắt, gán nhầm còn tệ hơn không gán.
    """
    if not os.path.exists(image_path):
        raise FileNotFoundError(f"Image not found: {image_path}")
    os.makedirs(output_dir, exist_ok=True)

    model, weights_used = load_model(weights_path)

    image = cv2.imread(image_path)
    if image is None:
        raise ValueError("Could not decode image file")

    image = letterbox(image, IMG_SIZE)

    # --- phân vùng mạch máu
    x = cv2.cvtColor(image, cv2.COLOR_BGR2RGB).astype(np.float32) / 255.0
    pred = model.predict(np.expand_dims(x, axis=0), verbose=0)[0]
    mask = (pred > 0.5)
    mask = mask[:, :, 0] if mask.ndim == 3 else mask

    # --- chuẩn hoá theo mắt CHỈ để phân tích vùng, KHÔNG đụng ảnh xuất ra.
    # Lỗi cũ: lật cả mask/image tại chỗ rồi lưu bản ĐÃ LẬT -> mask chồng lên ảnh
    # gốc mắt trái (OS) bị soi gương ngược. Nay giữ mask ở đúng chiều ảnh đầu vào
    # để lưu/hiển thị, và chỉ lật MỘT BẢN SAO để gán nhãn temporal/nasal cho
    # đồng nhất giữa hai mắt (trục nasal-temporal đảo chiều giữa OD và OS).
    eye_norm = (eye or "").strip().upper()
    flipped = eye_norm == "OS"

    mask_norm = np.fliplr(mask) if flipped else mask
    image_norm = np.fliplr(image) if flipped else image

    # --- FD toàn ảnh + lacunarity: bất biến với lật ngang (512 chia hết cho mọi
    # cạnh hộp) nên tính thẳng trên mask GỐC.
    fd_total, _ = box_count_fd(mask, BOX_SIZES_FULL)
    lac = lacunarity(mask)

    # --- loại đĩa thị rồi chia vùng, TẤT CẢ trong không gian đã chuẩn hoá.
    disc_y, disc_x = locate_optic_disc(mask_norm)
    mask_no_disc = remove_disc(mask_norm, disc_y, disc_x)

    cy, cx = fov_center(image_norm)
    quads = {}
    if eye_norm in ("OD", "OS"):
        for name, (sy, sx) in quadrant_slices(mask_norm.shape, cy, cx).items():
            fd, _ = box_count_fd(mask_no_disc[sy, sx], BOX_SIZES_QUAD)
            quads[name] = round(fd, 4) if fd is not None else None

    # discCenter/fovCenter suy trong không gian chuẩn hoá -> ánh xạ NGƯỢC về toạ
    # độ ảnh gốc để trùng với mask đã lưu (điểm x lật quanh mép phải).
    w_full = mask.shape[1]
    disc_x_orig = (w_full - 1 - disc_x) if flipped else disc_x
    cx_orig = (w_full - 1 - cx) if flipped else cx

    values = [v for v in quads.values() if v is not None]
    complete = len(values) == 4

    # Bất đối xứng giữa các vùng — chỉ dấu chính của hướng nghiên cứu này.
    fd_asym = round(float(np.std(values)), 4) if complete else None
    fd_tn = (
        round((quads["ST"] + quads["IT"]) / 2 - (quads["SN"] + quads["IN"]) / 2, 4)
        if complete else None
    )

    # --- ảnh xuất ra Ở ĐÚNG CHIỀU ẢNH GỐC (skeleton chỉ để hiển thị).
    # Dùng mask GỐC, không phải mask_norm, để chồng khớp ảnh đáy mắt đầu vào.
    skeleton = skeletonize(mask)
    mask_path = os.path.join(output_dir, "vessel_mask.png")
    skeleton_path = os.path.join(output_dir, "skeleton.png")
    cv2.imwrite(mask_path, (mask.astype(np.uint8) * 255))
    cv2.imwrite(skeleton_path, (skeleton.astype(np.uint8) * 255))

    return {
        "FD_total": round(fd_total, 4) if fd_total is not None else None,
        "FD_ST": quads.get("ST"),
        "FD_SN": quads.get("SN"),
        "FD_IT": quads.get("IT"),
        "FD_IN": quads.get("IN"),
        "FD_asym": fd_asym,
        "FD_TN": fd_tn,
        "lacunarity": round(lac, 4) if lac is not None else None,
        "vesselMaskPath": mask_path,
        "skeletonImagePath": skeleton_path,
        # Siêu dữ liệu để tái lập — BẮT BUỘC báo cáo kèm mọi giá trị FD.
        "meta": {
            "eye": eye_norm or None,
            "flipped": flipped,
            "imgSize": IMG_SIZE,
            "discRadius": DISC_RADIUS,
            "discCenter": [disc_y, disc_x_orig],
            "fovCenter": [cy, cx_orig],
            "boxSizesFull": BOX_SIZES_FULL,
            "boxSizesQuadrant": BOX_SIZES_QUAD,
            "computedOn": "mask",
            "weightsUsed": weights_used,
        },
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("image_path")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--eye", default=None, help="OD (mắt phải) hoặc OS (mắt trái)")
    args = parser.parse_args()

    try:
        result = predict(args.image_path, args.output_dir, args.eye)
        print(json.dumps({"module": "module3", "status": "success", "result": result}))
    except Exception as e:
        print(json.dumps({"module": "module3", "status": "error", "message": str(e)}))
        sys.exit(1)
