#!/usr/bin/env bash
# Khởi tạo / bảo trì CSDL cho DiaCompanion Lite (chạy trong container dbinit).
#   init             (mặc định) tạo DB → áp schema nếu DB trống → seed nếu chưa có tài khoản
#                    (+ tài khoản Research nếu đặt LITE_RESEARCH_EMAIL)
#   export           xuất dataset nghiên cứu Gap 2 (CSV, đã bỏ định danh) ra /exports/dataset.csv
#   reset-password   cấp lại mật khẩu tạm cho LITE_ADMIN_EMAIL
set -euo pipefail

: "${MSSQL_SA_PASSWORD:?Thiếu MSSQL_SA_PASSWORD}"
DB_HOST="${DB_HOST:-db}"
DB_NAME="${DB_NAME:-DiaCompanion}"
EMAIL="${LITE_ADMIN_EMAIL:-nghiencuu@benhvien.local}"
FULL_NAME="${LITE_ADMIN_NAME:-Nghiên cứu viên}"
LICENSE="${LITE_LICENSE:-NCKH-001}"
AI_DIR="${AI_DIR:-/ai_service}"
RESEARCH_EMAIL="${LITE_RESEARCH_EMAIL:-}"

# Email đi vào câu SQL: chỉ cho phép ký tự an toàn.
email_re='^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+$'
if [[ ! "$EMAIL" =~ $email_re ]]; then
  echo "LITE_ADMIN_EMAIL không hợp lệ: $EMAIL" >&2; exit 2
fi
if [ -n "$RESEARCH_EMAIL" ] && [[ ! "$RESEARCH_EMAIL" =~ $email_re ]]; then
  echo "LITE_RESEARCH_EMAIL không hợp lệ: $RESEARCH_EMAIL" >&2; exit 2
fi

SQLCMD=(/opt/mssql-tools18/bin/sqlcmd -C -b -S "$DB_HOST" -U sa -P "$MSSQL_SA_PASSWORD")
q() { "${SQLCMD[@]}" -d "$DB_NAME" -h -1 -W -Q "SET NOCOUNT ON; $1" | tr -d '[:space:]'; }

wait_db() {
  echo "Chờ SQL Server ($DB_HOST)…"
  for _ in $(seq 1 60); do
    if "${SQLCMD[@]}" -Q "SELECT 1" >/dev/null 2>&1; then return 0; fi
    sleep 3
  done
  echo "SQL Server không sẵn sàng sau 180 giây." >&2; exit 1
}

cmd="${1:-init}"
wait_db

case "$cmd" in
  init)
    "${SQLCMD[@]}" -Q "IF DB_ID(N'$DB_NAME') IS NULL CREATE DATABASE [$DB_NAME];"
    if [ "$(q "SELECT COUNT(*) FROM sys.tables WHERE name = 'Patients'")" = "0" ]; then
      echo "DB trống → áp schema…"
      "${SQLCMD[@]}" -d "$DB_NAME" -f 65001 -i /init/schema.sql
    else
      echo "Schema đã có — bỏ qua."
    fi
    if [ "$(q "SELECT COUNT(*) FROM dbo.Users WHERE Email = N'$EMAIL'")" = "0" ]; then
      echo "Chưa có tài khoản $EMAIL → seed…"
      extra=()
      [ "${ALLOW_MISSING_WEIGHTS:-0}" = "1" ] && extra+=(--allow-missing-weights)
      [ -n "$RESEARCH_EMAIL" ] && extra+=(--research-email "$RESEARCH_EMAIL")
      python3 /init/seed_lite.py init --email "$EMAIL" --full-name "$FULL_NAME" --license "$LICENSE" \
        --ai-service-dir "$AI_DIR" --out /tmp/lite_seed.sql --password-out /secrets/initial_credentials.txt "${extra[@]}"
      "${SQLCMD[@]}" -d "$DB_NAME" -f 65001 -i /tmp/lite_seed.sql
      rm -f /tmp/lite_seed.sql
      echo "================================================================"
      echo " Đăng nhập lần đầu: $EMAIL"
      echo " Mật khẩu tạm (đổi ngay khi đăng nhập): xem ./secrets/initial_credentials.txt"
      echo "================================================================"
    else
      echo "Tài khoản $EMAIL đã tồn tại — không seed lại."
    fi
    # DB đã seed từ trước (chưa có role/tài khoản Research): bổ sung, không đụng tài khoản bác sĩ.
    if [ -n "$RESEARCH_EMAIL" ] && [ "$(q "SELECT COUNT(*) FROM dbo.Users WHERE Email = N'$RESEARCH_EMAIL'")" = "0" ]; then
      echo "Chưa có tài khoản nghiên cứu $RESEARCH_EMAIL → thêm…"
      python3 /init/seed_lite.py add-research --email "$RESEARCH_EMAIL" --out /tmp/lite_research.sql --password-out /secrets/research_credentials.txt
      "${SQLCMD[@]}" -d "$DB_NAME" -f 65001 -i /tmp/lite_research.sql
      rm -f /tmp/lite_research.sql
      echo " Tài khoản nghiên cứu: $RESEARCH_EMAIL — mật khẩu tạm ở ./secrets/research_credentials.txt"
    fi
    echo "dbinit hoàn tất."
    ;;
  export)
    mkdir -p /exports
    # CSV cùng cột với endpoint web /api/research/dataset.csv. sqlcmd in NULL thành chữ "NULL" → đổi thành ô rỗng.
    {
      printf '\xEF\xBB\xBF'   # BOM để Excel mở đúng tiếng Việt
      "${SQLCMD[@]}" -d "$DB_NAME" -f 65001 -W -w 65535 -s "," -i /init/export_dataset.sql \
        | sed -e '2d' -e '/rows affected/d' -e '/^$/d' \
              -e ':a;s/,NULL,/,,/g;ta' -e 's/,NULL$/,/' -e 's/^NULL,/,/'
    } > /exports/dataset.csv
    echo "Đã ghi /exports/dataset.csv ($(( $(wc -l < /exports/dataset.csv) - 1 )) dòng dữ liệu)."
    ;;
  reset-password)
    python3 /init/seed_lite.py reset-password --email "$EMAIL" --out /tmp/lite_reset.sql --password-out /secrets/initial_credentials.txt
    "${SQLCMD[@]}" -d "$DB_NAME" -f 65001 -i /tmp/lite_reset.sql
    rm -f /tmp/lite_reset.sql
    echo "Đã cấp lại mật khẩu tạm — xem ./secrets/initial_credentials.txt"
    ;;
  *) echo "Lệnh không hỗ trợ: $cmd (init | export | reset-password)" >&2; exit 2 ;;
esac
