#!/usr/bin/env bash
# Sinh .env với mật khẩu/khóa ngẫu nhiên (chỉ ký tự chữ + số để an toàn trong chuỗi kết nối).
set -euo pipefail
cd "$(dirname "$0")/.."
[ -e .env ] && { echo ".env đã tồn tại — không ghi đè." >&2; exit 1; }
# head đóng ống sớm → tr nhận SIGPIPE; tắt pipefail trong hàm này.
rand() ( set +o pipefail; LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c "$1" )
sa="Aa1$(rand 21)"
{
  echo "MSSQL_SA_PASSWORD=$sa"
  echo "JWT_SIGNING_KEY=$(rand 64)"
  echo "LITE_ADMIN_EMAIL=${1:-bacsi@benhvien.local}"
  echo "LITE_ADMIN_NAME=Bác sĩ"
  echo "LITE_LICENSE=NCKH-001"
  echo "LITE_RESEARCH_EMAIL=${2:-nghiencuu@benhvien.local}"
  echo "WEB_PORT=8080"
  echo "ALLOW_MISSING_WEIGHTS=0"
} > .env
chmod 600 .env
echo "Đã tạo .env"
