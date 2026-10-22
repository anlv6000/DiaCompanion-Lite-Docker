#!/usr/bin/env bash
# Chạy bộ kiểm thử trên Docker trong project riêng "dr-ai-diagnosis-test" (bản Linux/macOS của run-tests.ps1).
#   docker/test/run-tests.sh [--keep] [--plan-only] [--clean] [--no-build] [--tester "Họ tên"] [--version 1.3.1] [--port 8090]
set -euo pipefail
cd "$(dirname "$0")/../.."
KEEP=0; PLAN=0; CLEAN=0; BUILD=1; PORT=8090
export APP_VERSION=1.3.1 TESTER_NAME="" GIT_COMMIT=""
while [ $# -gt 0 ]; do case "$1" in
  --keep) KEEP=1;; --plan-only) PLAN=1;; --clean) CLEAN=1;; --no-build) BUILD=0;;
  --tester) TESTER_NAME="$2"; shift;; --version) APP_VERSION="$2"; shift;; --port) PORT="$2"; shift;;
  *) echo "Tham số không hỗ trợ: $1" >&2; exit 2;; esac; shift; done
GIT_COMMIT="$(git rev-parse --short HEAD 2>/dev/null || true)"
ENVF=docker/test/.test.env
rand() ( set +o pipefail; LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c "$1" )
cat > "$ENVF" <<ENV
MSSQL_SA_PASSWORD=Aa1$(rand 21)
JWT_SIGNING_KEY=$(rand 64)
LITE_ADMIN_EMAIL=bacsi.test@test.local
LITE_ADMIN_NAME=Bác sĩ Kiểm thử
LITE_LICENSE=TEST-001
LITE_RESEARCH_EMAIL=nghiencuu.test@test.local
WEB_PORT=$PORT
ALLOW_MISSING_WEIGHTS=0
ENV
dc() { docker compose -p dr-ai-diagnosis-test --env-file "$ENVF" -f docker-compose.yml -f docker/test/docker-compose.test.yml "$@"; }
step() { printf '\n==> %s\n' "$1"; }

if [ $CLEAN = 1 ]; then step "Dọn môi trường test"; dc down -v --remove-orphans; exit 0; fi
mkdir -p test-run/report
if [ $PLAN = 1 ]; then step "Sinh kế hoạch kiểm thử"; dc build tester; dc run --rm --no-deps tester --plan-only; exit 0; fi

step "Dọn lần chạy trước"
dc down -v --remove-orphans
rm -rf test-run/secrets test-run/exports; mkdir -p test-run/secrets

step "Dựng môi trường test"
if [ $BUILD = 1 ]; then dc up -d --build db dbinit ai backend web; else dc up -d db dbinit ai backend web; fi
dc build tester   # mã test nằm trong image: luôn build lại (có cache)

step "Chờ backend sẵn sàng"
for i in $(seq 1 240); do
  init="$(dc ps -a -q dbinit | head -1)"
  if [ -n "$init" ]; then
    st="$(docker inspect -f '{{.State.Status}} {{.State.ExitCode}}' "$init")"
    case "$st" in "exited 0") ;; exited*) dc logs --tail 40 dbinit; echo "dbinit lỗi." >&2; exit 1;; esac
  fi
  be="$(dc ps -q backend | head -1)"
  [ -n "$be" ] && [ "$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$be")" = healthy ] && break
  [ "$i" = 240 ] && { dc ps; echo "Backend chưa sẵn sàng sau 20 phút." >&2; exit 1; }
  sleep 5
done

step "Tạo hai tài khoản người chấm"
for n in 1 2; do
  dc run --rm -e "GRADER_EMAIL=cham$n.test@test.local" -e "GRADER_NAME=Grader Test $n" dbinit add-grader
  cp test-run/secrets/grader_credentials.txt "test-run/secrets/grader$n.txt"
done

step "Chạy bộ test"
set +e; dc run --rm tester; code=$?; set -e
echo; ls -t test-run/report/Bao-cao-kiem-thu-*.docx 2>/dev/null | head -1 | sed 's/^/Tài liệu kiểm thử: /'
if [ $KEEP = 1 ]; then
  echo "Môi trường test đang chạy: http://localhost:$PORT — tài khoản: test-run/secrets/test_accounts.txt"
  echo "Dọn khi xong: docker/test/run-tests.sh --clean"
else
  step "Dọn môi trường test"; dc down -v --remove-orphans
fi
exit $code
