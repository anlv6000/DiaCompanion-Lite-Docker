# syntax=docker/dockerfile:1.6
# Dịch vụ AI (FastAPI + 3 model). Build context = thư mục gốc repo.
# Chạy CPU. Trọng số KHÔNG nằm trong image: gắn volume vào /app/models/model_N/weights.
FROM python:3.11-slim

# libgl1 + libglib2.0-0: cần cho opencv-python (bản không headless) trên image slim.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgl1 libglib2.0-0 \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
ENV PIP_NO_CACHE_DIR=1 PIP_DISABLE_PIP_VERSION_CHECK=1 PYTHONUNBUFFERED=1

# Phiên bản TensorFlow: model_3 nạp tệp .keras bằng tf.keras.models.load_model.
# 2.15 (Keras 2.15) là bản thấp nhất mà requirements.txt cho phép; đổi bằng
#   docker compose build --build-arg TF_VERSION=2.16.2 ai
# nếu model của bạn được huấn luyện bằng Keras 3.
ARG TF_VERSION=2.16.2
# TensorFlow 2.15 cần numpy < 2.
RUN printf 'numpy<2\n' > /constraints.txt
COPY ai_service/requirements.txt /tmp/requirements.txt
RUN pip install --constraint /constraints.txt torch torchvision --index-url https://download.pytorch.org/whl/cpu \
 && pip install --constraint /constraints.txt "tensorflow-cpu==${TF_VERSION}" \
 && grep -v -i -E '^(torch|torchvision|tensorflow)([<>=!~ ]|$)' /tmp/requirements.txt > /tmp/rest.txt \
 && pip install --constraint /constraints.txt -r /tmp/rest.txt \
 && python -c "import tensorflow as tf; assert tf.__version__ == '${TF_VERSION}', tf.__version__"

COPY ai_service/app.py ai_service/model_runners.py ./
COPY ai_service/models/model_1/predict.py models/model_1/predict.py
COPY ai_service/models/model_2/predict.py models/model_2/predict.py
COPY ai_service/models/model_3/predict.py models/model_3/predict.py

# UID 1654 trùng người dùng "app" của image backend (chung volume ảnh).
RUN useradd --uid 1654 --no-create-home --shell /usr/sbin/nologin app \
 && mkdir -p /data/storage && chown -R 1654:1654 /data /app
USER 1654

# FUNDUS_ROOT = thư mục CHA của "fundus" (backend lưu đường dẫn dạng fundus/2026/…).
ENV FUNDUS_ROOT=/data/storage \
    MASK_OUTPUT_ROOT=/data/storage/ai_masks
EXPOSE 8000
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000"]
