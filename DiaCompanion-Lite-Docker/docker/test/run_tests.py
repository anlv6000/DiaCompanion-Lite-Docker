"""Chạy bộ test API end-to-end và sinh tài liệu kiểm thử.

  python run_tests.py              chạy toàn bộ, ghi báo cáo vào $OUT_DIR
  python run_tests.py --plan-only  chỉ sinh kế hoạch/kịch bản kiểm thử (mọi case "CHƯA CHẠY")

Đầu ra trong $OUT_DIR:
  Bao-cao-kiem-thu-DR-AI-Diagnosis-<phiên bản>-<thời gian>.docx
  results.json, results.csv
Mã thoát: 0 nếu không có case LỖI, 1 nếu có.
"""
from __future__ import annotations

import csv
import datetime as dt
import json
import os
import sys
import time
import traceback

import requests

import cases
from cases import REGISTRY, Ctx
from report_docx import build_report

PASS, FAIL, BLOCKED = "ĐẠT", "LỖI", "BỊ CHẶN"


def run_all(ctx: Ctx) -> None:
    total = len(REGISTRY)
    for i, tc in enumerate(REGISTRY, 1):
        missing = [k for k in tc.needs if k not in ctx.s]
        t0 = time.time()
        if missing:
            tc.status = BLOCKED
            tc.actual = "Không chạy: thiếu dữ liệu từ case trước (" + ", ".join(missing) + ")"
        else:
            try:
                tc.actual = tc.fn(ctx) or ""
                tc.status = PASS
            except AssertionError as e:
                tc.status, tc.actual = FAIL, str(e)
            except requests.RequestException as e:
                tc.status, tc.actual = FAIL, f"Lỗi kết nối: {e.__class__.__name__}: {e}"
            except Exception as e:  # lỗi của chính bộ test hoặc dữ liệu bất thường
                tc.status = FAIL
                tc.actual = f"Lỗi không mong đợi: {e.__class__.__name__}: {e}"
                traceback.print_exc()
        tc.seconds = time.time() - t0
        mark = {PASS: "OK ", FAIL: "LỖI", BLOCKED: "CHẶN"}[tc.status]
        print(f"[{i:2}/{total}] {mark} {tc.id:8} {tc.title}\n             → {tc.actual}", flush=True)


def write_raw(out_dir: str, meta: dict) -> None:
    rows = [{"id": c.id, "module": cases.MODULES.get(c.module, c.module), "title": c.title,
             "expected": c.expected, "status": c.status, "actual": c.actual, "seconds": round(c.seconds, 2)}
            for c in REGISTRY]
    with open(os.path.join(out_dir, "results.json"), "w", encoding="utf-8") as f:
        json.dump({"meta": meta, "cases": rows}, f, ensure_ascii=False, indent=2)
    with open(os.path.join(out_dir, "results.csv"), "w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)


def main() -> int:
    plan_only = "--plan-only" in sys.argv
    out_dir = os.environ.get("OUT_DIR", "/work/report")
    os.makedirs(out_dir, exist_ok=True)
    started = dt.datetime.now()
    meta = {
        "app_version": os.environ.get("APP_VERSION", "1.3.1"),
        "git_commit": os.environ.get("GIT_COMMIT", ""),
        "tester": os.environ.get("TESTER_NAME", ""),
        "environment": os.environ.get("TEST_ENV_NAME", "Docker, project dr-ai-diagnosis-test (tách riêng khỏi bản dev và production)"),
        "api_url": os.environ.get("API_URL", ""),
        "started": started.strftime("%d/%m/%Y %H:%M:%S"),
        "plan_only": plan_only,
    }
    if not plan_only:
        ctx = Ctx()
        print(f"Bắt đầu {len(REGISTRY)} test case — API {ctx.api}\n", flush=True)
        run_all(ctx)
        try:
            with open(os.path.join(ctx.secrets_dir, "test_accounts.txt"), "w", encoding="utf-8") as f:
                f.write("Tài khoản của MÔI TRƯỜNG TEST (dữ liệu giả, xóa khi dọn môi trường):\n")
                for key, email, pw in ctx.s.get("accounts", []):
                    f.write(f"{key:9} {email:32} {pw}\n")
        except OSError as e:
            print(f"(không ghi được test_accounts.txt: {e})")
    finished = dt.datetime.now()
    meta["finished"] = finished.strftime("%d/%m/%Y %H:%M:%S")
    meta["duration"] = str(finished - started).split(".")[0]

    counts = {s: sum(c.status == s for c in REGISTRY) for s in (PASS, FAIL, BLOCKED)}
    meta["counts"] = counts
    write_raw(out_dir, meta)
    stamp = started.strftime("%Y%m%d-%H%M")
    kind = "Ke-hoach-kiem-thu" if plan_only else "Bao-cao-kiem-thu"
    path = os.path.join(out_dir, f"{kind}-DR-AI-Diagnosis-{meta['app_version']}-{stamp}.docx")
    build_report(path, REGISTRY, cases.MODULES, meta)

    print(f"\nĐẠT {counts[PASS]} · LỖI {counts[FAIL]} · BỊ CHẶN {counts[BLOCKED]} / {len(REGISTRY)}")
    print(f"Tài liệu: {path}")
    return 1 if counts[FAIL] or counts[BLOCKED] else 0


if __name__ == "__main__":
    sys.exit(main())
