"""Danh mục test case API end-to-end cho DR AI-Diagnosis (trước đây DiaCompanion Lite; bản nghiên cứu 1.3.1).

Mỗi test case là một hàm đăng ký bằng @case(...). Các case chạy TUẦN TỰ theo thứ tự khai báo
và dùng chung trạng thái (ctx.s): case sau dựa vào dữ liệu case trước tạo ra. Nếu dữ liệu cần
thiết không có (case trước lỗi), case bị đánh dấu BỊ CHẶN thay vì báo lỗi giả.
Hàm trả về một câu mô tả "kết quả thực tế"; lỗi kiểm tra ném AssertionError.
"""
from __future__ import annotations

import csv
import hashlib
import io
import os
import re
import secrets
import statistics
import json
import string
import threading
import time
import zipfile
from dataclasses import dataclass, field
from typing import Callable

import requests

# ----------------------------------------------------------------------------- khung

@dataclass
class Case:
    id: str
    module: str
    title: str
    expected: str
    fn: Callable
    needs: tuple = ()
    status: str = "CHƯA CHẠY"   # ĐẠT | LỖI | BỊ CHẶN | CHƯA CHẠY
    actual: str = ""
    seconds: float = 0.0


REGISTRY: list[Case] = []

MODULES = {
    "ENV": "Môi trường",
    "AUTH": "Đăng nhập & mật khẩu",
    "RBAC": "Phân quyền theo vai trò",
    "PAT": "Hồ sơ bệnh nhân ẩn danh",
    "VIS": "Lượt khám & chỉ số lâm sàng",
    "IMG": "Nạp ảnh đáy mắt",
    "BLD": "Chấm mù vòng 1 (trước AI)",
    "CTX": "Thông tin lâm sàng cạnh ảnh",
    "AI": "Phân tích AI",
    "REV": "Xác nhận lâm sàng",
    "GRD": "Chấm độc lập vòng 2 & phân xử vòng 3",
    "EXP": "Kết xuất dữ liệu nghiên cứu",
    "PKG": "Chấm mù trên nhiều máy (gói offline, 1.3.1)",
}


def case(id: str, title: str, expected: str, needs: tuple = ()):
    def deco(fn):
        REGISTRY.append(Case(id, id.split("-")[0], title, expected, fn, needs))
        return fn
    return deco


class Ctx:
    def __init__(self):
        self.api = os.environ.get("API_URL", "http://backend:8080").rstrip("/")
        self.web = os.environ.get("WEB_URL", "http://web").rstrip("/")
        self.ai = os.environ.get("AI_URL", "http://ai:8000").rstrip("/")
        self.secrets_dir = os.environ.get("SECRETS_DIR", "/work/secrets")
        self.fixtures = os.environ.get("FIXTURES_DIR", os.path.join(os.path.dirname(__file__), "fixtures"))
        self.ai_wait = int(os.environ.get("AI_WAIT_SECONDS", "1200"))
        self.ai_timeout = int(os.environ.get("AI_TIMEOUT_SECONDS", "900"))
        self.tokens: dict[str, str] = {}
        self.s: dict = {}       # trạng thái dùng chung giữa các case
        self.http = requests.Session()

    # ---- HTTP
    def call(self, who: str | None, method: str, path: str, timeout: float = 60, **kw) -> requests.Response:
        headers = kw.pop("headers", {})
        if who:
            headers["Authorization"] = f"Bearer {self.tokens[who]}"
        url = path if path.startswith("http") else self.api + path
        r = self.http.request(method, url, headers=headers, timeout=timeout, **kw)
        # Đăng nhập bị giới hạn 5 lần/phút/IP (cấu hình thật của backend, giữ nguyên). Bộ test đăng nhập
        # nhiều tài khoản liên tiếp nên gặp 429 là bình thường: chờ cửa sổ giới hạn trôi qua rồi gửi lại.
        tries = 0
        while r.status_code == 429 and path.endswith("/api/auth/login") and tries < 3:
            tries += 1
            print(f"             (giới hạn đăng nhập 5 lần/phút: chờ 65 s rồi thử lại, lần {tries})", flush=True)
            time.sleep(65)
            r = self.http.request(method, url, headers=headers, timeout=timeout, **kw)
        return r

    def fixture(self, name: str) -> bytes:
        with open(os.path.join(self.fixtures, name), "rb") as f:
            return f.read()

    # ---- tài khoản do dbinit tạo (mật khẩu tạm)
    def credentials(self, filename: str) -> list[tuple[str, str]]:
        path = os.path.join(self.secrets_dir, filename)
        with open(path, encoding="utf-8") as f:
            text = f.read()
        return re.findall(r"Email:\s*(\S+)\s*\n\s*Mật khẩu tạm:\s*(\S+)", text)


def expect(r: requests.Response, *codes: int) -> requests.Response:
    if r.status_code not in codes:
        body = r.text[:300].replace("\n", " ")
        raise AssertionError(f"HTTP {r.status_code} (mong đợi {'/'.join(map(str, codes))}): {body}")
    return r


def check(cond: bool, msg: str):
    if not cond:
        raise AssertionError(msg)


def msg_of(r: requests.Response) -> str:
    try:
        j = r.json()
        return j.get("message") or j.get("title") or ""
    except Exception:
        return r.text[:120]


def sha256(b: bytes) -> str:
    return hashlib.sha256(b).hexdigest()


def strong_password() -> str:
    alphabet = string.ascii_letters + string.digits
    return "Kt9" + "".join(secrets.choice(alphabet) for _ in range(13)) + "a7"


def read_csv(r: requests.Response) -> tuple[bytes, list[str], list[dict]]:
    raw = r.content
    text = raw.decode("utf-8-sig")
    reader = csv.DictReader(io.StringIO(text))
    return raw, list(reader.fieldnames or []), list(reader)


TEST_CODE = "NC-TEST-001"
TEST_CODE_2 = "NC-TEST-002"
DOCTOR_NAME_MARK = os.environ.get("DOCTOR_NAME", "Bác sĩ Kiểm thử")
DR_THRESHOLDS = [0.481, 1.468, 2.286, 3.469]

# ============================================================================ ENV

@case("ENV-01", "Backend sống và kết nối được CSDL (GET /health)",
      "HTTP 200, status = healthy, database = true")
def env_01(c: Ctx):
    j = expect(c.call(None, "GET", "/health"), 200).json()
    check(j.get("status") == "healthy" and j.get("database") is True, f"Phản hồi: {j}")
    return f"status={j['status']}, database={j['database']}"


@case("ENV-02", "Web (nginx) phục vụ giao diện và chuyển tiếp /health tới backend",
      "GET / trả trang HTML có <div id=\"root\">; GET /health qua nginx trả 200")
def env_02(c: Ctx):
    r = expect(c.call(None, "GET", c.web + "/"), 200)
    check('id="root"' in r.text, "Trang chủ không có phần tử root")
    expect(c.call(None, "GET", c.web + "/health"), 200)
    return "Trang chủ HTML OK; /health qua nginx 200"


@case("ENV-03", "Dịch vụ AI đã nạp xong 3 model",
      "GET ai:8000/health trả models_ready = true (chờ tối đa AI_WAIT_SECONDS)")
def env_03(c: Ctx):
    t0 = time.time()
    last = None
    while time.time() - t0 < c.ai_wait:
        try:
            last = c.call(None, "GET", c.ai + "/health", timeout=10).json()
            if last.get("models_ready", True):
                return f"models_ready=true sau {time.time() - t0:.0f} s"
        except requests.RequestException as e:
            last = str(e)
        time.sleep(10)
    raise AssertionError(f"Quá {c.ai_wait} s vẫn chưa sẵn sàng: {last}")

# =========================================================================== AUTH

@case("AUTH-01", "Đăng nhập sai mật khẩu", "HTTP 401")
def auth_01(c: Ctx):
    email, _ = c.credentials("initial_credentials.txt")[0]
    c.s["doctor_email"] = email
    r = c.call(None, "POST", "/api/auth/login", json={"email": email, "password": "SaiMatKhau123"})
    expect(r, 401)
    return "HTTP 401 — " + msg_of(r)


@case("AUTH-02", "Đăng nhập bằng mật khẩu tạm buộc đổi mật khẩu trước khi dùng API",
      "Đăng nhập 200 với mustChangePassword = true; gọi /api/patients bằng token này bị 403 MSG-MUST-CHANGE-PASSWORD")
def auth_02(c: Ctx):
    email, pw = c.credentials("initial_credentials.txt")[0]
    j = expect(c.call(None, "POST", "/api/auth/login", json={"email": email, "password": pw}), 200).json()
    check(j.get("mustChangePassword") is True, "mustChangePassword không phải true")
    c.tokens["doctor_tmp"] = j["token"]
    c.s["doctor_tmp"] = True
    r = c.call("doctor_tmp", "GET", "/api/patients?page=1&pageSize=5")
    expect(r, 403)
    check(r.json().get("messageCode") == "MSG-MUST-CHANGE-PASSWORD", f"messageCode: {r.text[:120]}")
    return "Đăng nhập 200, mustChangePassword=true; API khác bị 403 MSG-MUST-CHANGE-PASSWORD"


@case("AUTH-03", "Đổi sang mật khẩu yếu (\"abc\")", "HTTP 400 (mật khẩu ≥ 8 ký tự, có chữ và số)",
      needs=("doctor_tmp",))
def auth_03(c: Ctx):
    r = c.call("doctor_tmp", "POST", "/api/auth/change-password", json={"newPassword": "abc"})
    expect(r, 400)
    return "HTTP 400 — " + msg_of(r)


def _activate(c: Ctx, key: str, email: str, temp_pw: str) -> int:
    j = expect(c.call(None, "POST", "/api/auth/login", json={"email": email, "password": temp_pw}), 200).json()
    c.tokens[key] = j["token"]
    new_pw = strong_password()
    expect(c.call(key, "POST", "/api/auth/change-password", json={"newPassword": new_pw}), 200)
    j = expect(c.call(None, "POST", "/api/auth/login", json={"email": email, "password": new_pw}), 200).json()
    check(j.get("mustChangePassword") is False, f"{email}: vẫn còn mustChangePassword")
    c.tokens[key] = j["token"]
    # Môi trường test dùng một lần: ghi lại mật khẩu mới để làm checklist giao diện thủ công (chạy với -Keep).
    c.s.setdefault("accounts", []).append((key, email, new_pw))
    return j["userId"]


@case("AUTH-04", "Bác sĩ đổi mật khẩu tạm rồi đăng nhập lại bằng mật khẩu mới",
      "Đổi mật khẩu 200; đăng nhập lại 200 với mustChangePassword = false", needs=("doctor_email",))
def auth_04(c: Ctx):
    email, pw = c.credentials("initial_credentials.txt")[0]
    c.s["doctor_id"] = _activate(c, "doctor", email, pw)
    return f"Đăng nhập lại OK (userId={c.s['doctor_id']})"


@case("AUTH-05", "Kích hoạt tài khoản nghiên cứu và hai người chấm độc lập",
      "Ba tài khoản đổi được mật khẩu tạm và đăng nhập lại bình thường")
def auth_05(c: Ctx):
    creds = c.credentials("initial_credentials.txt")
    check(len(creds) >= 2, "initial_credentials.txt không có tài khoản nghiên cứu (LITE_RESEARCH_EMAIL)")
    _activate(c, "research", *creds[1])
    g1 = c.credentials("grader1.txt")[0]
    g2 = c.credentials("grader2.txt")[0]
    c.s["grader1_id"] = _activate(c, "grader1", *g1)
    c.s["grader2_id"] = _activate(c, "grader2", *g2)
    c.s["roles_ready"] = True
    return f"Nghiên cứu {creds[1][0]}, người chấm {g1[0]} và {g2[0]} đều đăng nhập được"


@case("AUTH-06", "Gọi API không kèm token", "HTTP 401")
def auth_06(c: Ctx):
    expect(c.call(None, "GET", "/api/patients?page=1&pageSize=5"), 401)
    return "HTTP 401"

# =========================================================================== RBAC

def _forbid(c: Ctx, who: str, method: str, path: str):
    r = c.call(who, method, path)
    expect(r, 403)
    return f"HTTP 403 ({who} → {method} {path})"


@case("RBAC-01", "Người chấm không xem được danh sách bệnh nhân", "HTTP 403", needs=("roles_ready",))
def rbac_01(c): return _forbid(c, "grader1", "GET", "/api/patients?page=1&pageSize=5")


@case("RBAC-02", "Tài khoản nghiên cứu không xem được danh sách bệnh nhân", "HTTP 403", needs=("roles_ready",))
def rbac_02(c): return _forbid(c, "research", "GET", "/api/patients?page=1&pageSize=5")


@case("RBAC-03", "Bác sĩ không tải được dataset nghiên cứu", "HTTP 403", needs=("doctor_id",))
def rbac_03(c): return _forbid(c, "doctor", "GET", "/api/research/dataset.csv")


@case("RBAC-04", "Người chấm không tải được bảng chấm mù", "HTTP 403", needs=("roles_ready",))
def rbac_04(c): return _forbid(c, "grader1", "GET", "/api/research/grading.csv")


@case("RBAC-05", "Bác sĩ không vào được hàng chờ chấm vòng 2", "HTTP 403", needs=("doctor_id",))
def rbac_05(c): return _forbid(c, "doctor", "GET", "/api/grading/queue/2")


@case("RBAC-06", "Tài khoản nghiên cứu xem được tiến độ chấm mù", "HTTP 200", needs=("roles_ready",))
def rbac_06(c):
    expect(c.call("research", "GET", "/api/grading/progress"), 200)
    return "HTTP 200"

# ============================================================================ PAT

def _new_patient(c: Ctx, code: str, birth_year: int) -> dict:
    last = None
    for _ in range(3):  # SĐT giả ngẫu nhiên có thể trùng: thử lại như giao diện
        phone = "00" + "".join(secrets.choice(string.digits) for _ in range(9))
        r = c.call("doctor", "POST", "/api/patients", json={
            "fullName": code, "gender": 0, "dateOfBirth": f"{birth_year}-07-01", "phone": phone,
            "address": None, "diabetesType": 2, "diabetesDurationYears": 12, "baselineHbA1c": 7.9,
            "note": "Hồ sơ kiểm thử tự động", "createAccount": False, "existingUserId": None})
        if r.status_code != 409:
            j = expect(r, 200, 201).json()
            p = j.get("patient", j)
            p["_phone"] = phone
            return p
        last = r
    raise AssertionError("Trùng SĐT 3 lần liên tiếp: " + msg_of(last))


@case("PAT-01", "Tạo hồ sơ ẩn danh: mã nghiên cứu + năm sinh, SĐT giả 00xxxxxxxxx",
      "HTTP 200, trả Id và mã bệnh nhân hệ thống (BN…)", needs=("doctor_id",))
def pat_01(c: Ctx):
    p = _new_patient(c, TEST_CODE, 1968)
    c.s["p1"] = p["id"]
    c.s["p1_phone"] = p["_phone"]
    c.s["p1_code"] = p.get("code", "")
    return f"Id={p['id']}, mã={p.get('code')}"


@case("PAT-02", "Tạo hồ sơ thứ hai trùng số điện thoại", "HTTP 409 (SĐT đã dùng)", needs=("p1",))
def pat_02(c: Ctx):
    r = c.call("doctor", "POST", "/api/patients", json={
        "fullName": "NC-TEST-DUP", "gender": 1, "dateOfBirth": "1970-07-01", "phone": c.s["p1_phone"],
        "diabetesType": 2, "createAccount": False})
    expect(r, 409)
    return "HTTP 409 — " + msg_of(r)


@case("PAT-03", "Đọc lại hồ sơ vừa tạo", "Họ tên = mã nghiên cứu; ngày sinh = 01/07 của năm sinh", needs=("p1",))
def pat_03(c: Ctx):
    p = expect(c.call("doctor", "GET", f"/api/patients/{c.s['p1']}"), 200).json()
    check(p.get("fullName") == TEST_CODE, f"fullName = {p.get('fullName')}")
    check(str(p.get("dateOfBirth", "")).startswith("1968-07-01"), f"dateOfBirth = {p.get('dateOfBirth')}")
    return f"fullName={p['fullName']}, dateOfBirth={p['dateOfBirth']}"

# ============================================================================ VIS

@case("VIS-01", "Mở lượt khám cho bệnh nhân", "HTTP 200, lượt ở trạng thái đang khám (status = 0)", needs=("p1",))
def vis_01(c: Ctx):
    v = expect(c.call("doctor", "POST", "/api/visits", json={"patientId": c.s["p1"], "doctorId": c.s["doctor_id"]}), 200, 201).json()
    check(v.get("status") == 0, f"status = {v.get('status')}")
    c.s["v1"] = v["id"]
    return f"Lượt Id={v['id']}, status=0"


@case("VIS-02", "Mở lượt thứ hai khi lượt cũ chưa đóng", "HTTP 400", needs=("v1",))
def vis_02(c: Ctx):
    r = c.call("doctor", "POST", "/api/visits", json={"patientId": c.s["p1"], "doctorId": c.s["doctor_id"]})
    expect(r, 400)
    return "HTTP 400 — " + msg_of(r)


@case("VIS-03", "Lưu chỉ số lâm sàng của lượt (HbA1c 8.2, HA 150/90, đường huyết 9.1 trước ăn)",
      "HTTP 200; đọc lại đúng giá trị đã lưu", needs=("v1",))
def vis_03(c: Ctx):
    body = {"glucose": 9.1, "glucoseContext": 1, "glucoseNote": None, "glucoseRowVersion": None,
            "hbA1c": 8.2, "hbA1cNote": None, "hbA1cRowVersion": None,
            "systolicBp": 150, "diastolicBp": 90, "bloodPressureNote": None,
            "systolicRowVersion": None, "diastolicRowVersion": None}
    expect(c.call("doctor", "PUT", f"/api/visits/{c.s['v1']}/health-metrics", json=body), 200)
    m = expect(c.call("doctor", "GET", f"/api/visits/{c.s['v1']}/health-metrics"), 200).json()
    bp = m.get("bloodPressure") or {}
    sys_v = bp.get("systolicValue") if bp.get("systolicValue") is not None else (bp.get("value") if bp.get("metricType") == 3 else None)
    dia_v = bp.get("diastolicValue") if bp.get("diastolicValue") is not None else (bp.get("value") if bp.get("metricType") == 4 else None)
    got = (float(m["hbA1c"]["value"]), float(sys_v), float(dia_v), float(m["glucose"]["value"]), m["glucose"].get("context"))
    check(got == (8.2, 150.0, 90.0, 9.1, 1), f"Đọc lại: {got}")
    c.s["metrics_ok"] = True
    return "HbA1c 8.2; HA 150/90; đường huyết 9.1 (trước ăn)"


@case("VIS-04", "Lưu HbA1c ngoài khoảng cho phép (25%)", "HTTP 400", needs=("v1",))
def vis_04(c: Ctx):
    r = c.call("doctor", "PUT", f"/api/visits/{c.s['v1']}/health-metrics", json={"hbA1c": 25})
    expect(r, 400)
    return "HTTP 400 — " + msg_of(r)

# ============================================================================ IMG

def _upload(c: Ctx, patient: int, visit: int, eye: int, name: str, data: bytes, mime: str):
    return c.call("doctor", "POST", "/api/images", timeout=120,
                  files={"file": (name, data, mime)},
                  data={"patientId": str(patient), "visitId": str(visit), "eye": str(eye)})


@case("IMG-01", "Nạp ảnh mắt phải (JPG)", "HTTP 200; ảnh ở trạng thái chờ duyệt (qualityStatus = 0)", needs=("v1",))
def img_01(c: Ctx):
    data = c.fixture("fundus_od.jpg")
    img = expect(_upload(c, c.s["p1"], c.s["v1"], 0, "fundus_od.jpg", data, "image/jpeg"), 200, 201).json()
    check(img.get("qualityStatus") == 0, f"qualityStatus = {img.get('qualityStatus')}")
    c.s["od"] = img["id"]
    c.s["od_sha"] = sha256(data)
    return f"Ảnh OD Id={img['id']}"


@case("IMG-02", "Nạp tệp không phải ảnh (.txt)", "HTTP 400 (chỉ nhận JPG/PNG)", needs=("v1",))
def img_02(c: Ctx):
    r = _upload(c, c.s["p1"], c.s["v1"], 1, "not_an_image.txt", c.fixture("not_an_image.txt"), "text/plain")
    expect(r, 400)
    return "HTTP 400 — " + msg_of(r)


@case("IMG-03", "Nạp ảnh mắt trái (PNG)", "HTTP 200", needs=("v1",))
def img_03(c: Ctx):
    data = c.fixture("fundus_os.png")
    img = expect(_upload(c, c.s["p1"], c.s["v1"], 1, "fundus_os.png", data, "image/png"), 200, 201).json()
    c.s["os"] = img["id"]
    c.s["os_sha"] = sha256(data)
    return f"Ảnh OS Id={img['id']}"


@case("IMG-05", "Nạp thêm ảnh cho mắt phải khi mắt đó đã có ảnh (lỗi bấm hai lần ở bản ≤ 1.3.0)",
      "HTTP 409; thông báo hướng dẫn dùng \"Thay ảnh\"; lượt vẫn chỉ có một ảnh OD", needs=("od",))
def img_05(c: Ctx):
    r = _upload(c, c.s["p1"], c.s["v1"], 0, "fundus_od.jpg", c.fixture("fundus_od.jpg"), "image/jpeg")
    expect(r, 409)
    check("Thay ảnh" in msg_of(r), f"Thông báo: {msg_of(r)}")
    imgs = expect(c.call("doctor", "GET", f"/api/images?visitId={c.s['v1']}"), 200).json()
    od = [x for x in imgs if x["eye"] == 0]
    check(len(od) == 1, f"Lượt có {len(od)} ảnh OD")
    return "HTTP 409: " + msg_of(r)

# ============================================================================ BLD

def _image(c: Ctx, image_id: int) -> dict:
    return expect(c.call("doctor", "GET", f"/api/images/{image_id}"), 200).json()


@case("BLD-01", "Chạy AI khi ảnh chưa được chấm mù vòng 1",
      "Bị từ chối (HTTP 400); AI không chạy", needs=("od",))
def bld_01(c: Ctx):
    r = c.call("doctor", "POST", f"/api/diagnoses/run/{c.s['od']}", timeout=60)
    expect(r, 400)
    return "HTTP 400 — " + msg_of(r)


@case("BLD-02", "Gửi chấm mù không hợp lệ (chấm được nhưng không chọn độ)", "HTTP 400", needs=("od",))
def bld_02(c: Ctx):
    r = c.call("doctor", "POST", f"/api/grading/round1/{c.s['od']}", json={"gradable": True, "grade": None})
    expect(r, 400)
    return "HTTP 400 — " + msg_of(r)


@case("BLD-03", "Bác sĩ chấm mù ảnh OD: chấm được, DR nhẹ (độ 1)",
      "HTTP 200; ảnh tự chuyển sang Đạt (qualityStatus = 1)", needs=("od",))
def bld_03(c: Ctx):
    g = expect(c.call("doctor", "POST", f"/api/grading/round1/{c.s['od']}", json={"gradable": True, "grade": 1, "note": "kiểm thử"}), 200, 201).json()
    check(g.get("round") == 1 and g.get("grade") == 1, f"Phản hồi: {g}")
    q = _image(c, c.s["od"]).get("qualityStatus")
    check(q == 1, f"qualityStatus = {q}")
    c.s["od_r1"] = True
    return "Lưu vòng 1 độ 1; qualityStatus=1"


@case("BLD-04", "Chấm lại vòng 1 cho cùng ảnh", "HTTP 409 (phân độ chấm mù không sửa được)", needs=("od_r1",))
def bld_04(c: Ctx):
    r = c.call("doctor", "POST", f"/api/grading/round1/{c.s['od']}", json={"gradable": True, "grade": 3})
    expect(r, 409)
    return "HTTP 409 — " + msg_of(r)


@case("BLD-05", "Bác sĩ chấm mù ảnh OS: không chấm được",
      "HTTP 200; ảnh chuyển sang Không đạt (qualityStatus = 2)", needs=("os",))
def bld_05(c: Ctx):
    expect(c.call("doctor", "POST", f"/api/grading/round1/{c.s['os']}", json={"gradable": False, "grade": None, "note": "ảnh mờ"}), 200, 201)
    q = _image(c, c.s["os"]).get("qualityStatus")
    check(q == 2, f"qualityStatus = {q}")
    c.s["os_r1"] = True
    return "Lưu vòng 1 không chấm được; qualityStatus=2"


@case("BLD-06", "Chạy AI trên ảnh không chấm được", "HTTP 400 (ảnh không đạt chất lượng)", needs=("os_r1",))
def bld_06(c: Ctx):
    r = c.call("doctor", "POST", f"/api/diagnoses/run/{c.s['os']}", timeout=60)
    expect(r, 400)
    return "HTTP 400 — " + msg_of(r)

# ============================================================================ CTX

@case("CTX-01", "Bác sĩ xem thông tin lâm sàng cạnh ảnh OD",
      "Có mã nghiên cứu, ngày khám, tuổi, HbA1c 8.2, HA 150/90", needs=("od", "metrics_ok"))
def ctx_01(c: Ctx):
    j = expect(c.call("doctor", "GET", f"/api/grading/images/{c.s['od']}/context"), 200).json()
    check(j.get("researchCode") == TEST_CODE, f"researchCode = {j.get('researchCode')}")
    check(j.get("visitDate"), "Thiếu visitDate")
    check(float(j.get("hbA1c") or 0) == 8.2 and float(j.get("systolicBp") or 0) == 150, f"Chỉ số: {j}")
    check(j.get("birthYear") == 1968 and j.get("ageAtVisit") is not None, f"Tuổi: {j.get('birthYear')}/{j.get('ageAtVisit')}")
    return f"researchCode={j['researchCode']}, tuổi {j['ageAtVisit']}, HbA1c {j['hbA1c']}, HA {j['systolicBp']}/{j['diastolicBp']}"

# ============================================================================= AI

@case("AI-01", "Chạy 3 model AI cho ảnh OD đã chấm mù",
      "HTTP 200; drGrade trong 0–4; có điểm nguy cơ, ngưỡng hiệu lực và quyết định hoãn (độ tin cậy không trả qua API, kiểm ở EXP-05)",
      needs=("od_r1",))
def ai_01(c: Ctx):
    t0 = time.time()
    d = expect(c.call("doctor", "POST", f"/api/diagnoses/run/{c.s['od']}", timeout=c.ai_timeout + 60), 200, 201).json()
    check(d.get("drGrade") in (0, 1, 2, 3, 4), f"drGrade = {d.get('drGrade')}")
    check(d.get("clinicalRiskScore") is not None, "Thiếu clinicalRiskScore (bệnh nhân có HbA1c, huyết áp)")
    thr = d.get("effectiveDisagreementThreshold")
    check(thr is not None and 0 < float(thr) <= 1, f"effectiveDisagreementThreshold = {thr}")
    check(isinstance(d.get("isDeferred"), bool), f"isDeferred = {d.get('isDeferred')}")
    check(d.get("disagreement") is not None or d.get("isDeferred"), "Không có disagreement nhưng cũng không hoãn")
    conf = float(d.get("disagreement") or 0)
    c.s["dx"] = d["id"]
    c.s["dx_grade"] = d["drGrade"]
    c.s["dx_rv"] = d.get("rowVersion")
    return (f"drGrade={d['drGrade']}, nguy cơ={d['clinicalRiskScore']}, bất đồng={conf:.3f}, "
            f"ngưỡng={float(thr):.3f}, hoãn={d['isDeferred']}, mất {time.time() - t0:.0f} s")

# ============================================================================ REV

def _visit(c: Ctx, visit_id: int) -> dict:
    return expect(c.call("doctor", "GET", f"/api/visits/{visit_id}"), 200).json()


def _close(c: Ctx, visit_id: int):
    v = _visit(c, visit_id)
    return c.call("doctor", "PUT", f"/api/visits/{visit_id}/close", json={
        "conclusion": "Kết luận kiểm thử tự động", "referral": 1, "recheckMonths": None, "rowVersion": v["rowVersion"]})


@case("VIS-05", "Đóng lượt khi kết quả AI chưa được bác sĩ xác nhận", "HTTP 400", needs=("dx",))
def vis_05(c: Ctx):
    r = _close(c, c.s["v1"])
    expect(r, 400)
    return "HTTP 400 — " + msg_of(r)


@case("REV-01", "Ghi đè phân độ AI nhưng không nhập lý do", "HTTP 400", needs=("dx",))
def rev_01(c: Ctx):
    r = c.call("doctor", "POST", f"/api/triage/{c.s['dx']}/override",
               json={"rowVersion": c.s["dx_rv"], "finalGrade": 2, "reason": "  "})
    expect(r, 400)
    return "HTTP 400 — " + msg_of(r)


@case("REV-02", "Bác sĩ ghi đè phân độ AI có lý do (quyết định lâm sàng sau khi xem AI)",
      "HTTP 200; quyết định được lưu riêng, không ghi đè phân độ chấm mù", needs=("dx",))
def rev_02(c: Ctx):
    g = c.s["dx_grade"]
    final = g + 1 if g < 4 else 3
    d = expect(c.call("doctor", "GET", f"/api/diagnoses/{c.s['dx']}"), 200).json()
    expect(c.call("doctor", "POST", f"/api/triage/{c.s['dx']}/override",
                  json={"rowVersion": d.get("rowVersion"), "finalGrade": final, "reason": "Kiểm thử: chọn độ khác AI"}), 200, 201)
    r1 = expect(c.call("doctor", "GET", f"/api/grading/round1/{c.s['od']}"), 200).json()
    check(r1.get("grade") == 1, f"Phân độ chấm mù bị thay đổi: {r1}")
    c.s["override_grade"] = final
    c.s["reviewed"] = True
    return f"AI {g} → bác sĩ {final}; chấm mù vẫn là 1"


@case("REV-03", "Chạy lại AI sau khi bác sĩ đã xác nhận", "HTTP 409", needs=("reviewed",))
def rev_03(c: Ctx):
    r = c.call("doctor", "POST", f"/api/diagnoses/run/{c.s['od']}", timeout=60)
    expect(r, 409)
    return "HTTP 409 — " + msg_of(r)


@case("VIS-06", "Đóng lượt khám sau khi đã xác nhận đủ", "HTTP 200; status = 1", needs=("reviewed",))
def vis_06(c: Ctx):
    v = expect(_close(c, c.s["v1"]), 200).json()
    check(v.get("status") == 1, f"status = {v.get('status')}")
    c.s["v1_closed"] = True
    return "Đã đóng lượt (status=1)"


@case("IMG-04", "Nạp ảnh vào lượt đã đóng", "HTTP 409", needs=("v1_closed",))
def img_04(c: Ctx):
    r = _upload(c, c.s["p1"], c.s["v1"], 1, "fundus_os.png", c.fixture("fundus_os.png"), "image/png")
    expect(r, 409)
    return "HTTP 409 — " + msg_of(r)


@case("RBAC-07", "Người chấm không xem được kết quả AI", "HTTP 403", needs=("dx", "roles_ready"))
def rbac_07(c): return _forbid(c, "grader1", "GET", f"/api/diagnoses/by-image/{c.s['od']}")

# ============================================================== GRD (vòng 2/3)

@case("GRD-00", "Chuẩn bị: hồ sơ thứ hai nạp LẠI đúng tệp ảnh OD, không chấm mù",
      "Nạp được; dùng để thử phát hiện ảnh trùng và ảnh ngoài bộ chấm mù", needs=("od",))
def grd_00(c: Ctx):
    p = _new_patient(c, TEST_CODE_2, 1975)
    v = expect(c.call("doctor", "POST", "/api/visits", json={"patientId": p["id"], "doctorId": c.s["doctor_id"]}), 200, 201).json()
    img = expect(_upload(c, p["id"], v["id"], 0, "fundus_od_copy.jpg", c.fixture("fundus_od.jpg"), "image/jpeg"), 200, 201).json()
    c.s["dup"] = img["id"]
    return f"Bệnh nhân {p['id']}, ảnh trùng Id={img['id']}"


def _queue(c: Ctx, who: str, rnd: int) -> list[int]:
    j = expect(c.call(who, "GET", f"/api/grading/queue/{rnd}"), 200).json()
    return [x["imageId"] for x in j.get("items", [])]


@case("GRD-01", "Hàng chờ vòng 2 của người chấm 1",
      "Có ảnh OD; KHÔNG có ảnh OS (không chấm được) và ảnh chưa chấm vòng 1", needs=("od_r1", "os_r1", "dup", "roles_ready"))
def grd_01(c: Ctx):
    q = _queue(c, "grader1", 2)
    check(c.s["od"] in q, f"Thiếu OD trong hàng chờ {q}")
    check(c.s["os"] not in q and c.s["dup"] not in q, f"Hàng chờ có ảnh không đúng: {q}")
    return f"Hàng chờ: {q}"


@case("GRD-02", "Người chấm tải ảnh OD qua endpoint chấm",
      "HTTP 200; nội dung trùng khớp tệp đã nạp (SHA-256)", needs=("od", "roles_ready"))
def grd_02(c: Ctx):
    r = expect(c.call("grader1", "GET", f"/api/grading/images/{c.s['od']}/content"), 200)
    check(sha256(r.content) == c.s["od_sha"], "SHA-256 ảnh tải về khác tệp gốc")
    return f"{len(r.content)} byte, SHA-256 khớp"


@case("GRD-03", "Người chấm xem thông tin lâm sàng của ảnh OD (ảnh thuộc bộ chấm)",
      "HTTP 200, bản ẩn danh (1.3.1): có tuổi, HbA1c 8.2, HA 150/90; KHÔNG có mã nghiên cứu, ngày khám, năm sinh, ghi chú",
      needs=("od", "metrics_ok", "roles_ready"))
def grd_03(c: Ctx):
    j = expect(c.call("grader1", "GET", f"/api/grading/images/{c.s['od']}/context"), 200).json()
    check(j.get("hbA1c") == 8.2 and j.get("systolicBp") == 150 and j.get("ageAtVisit"), f"Chỉ số: {j}")
    hidden = {k: j.get(k) for k in ("researchCode", "visitDate", "patientNote")}
    check(all(v is None for v in hidden.values()) and not j.get("birthYear"), f"Lộ thông tin: {hidden}, birthYear={j.get('birthYear')}")
    check(TEST_CODE not in json.dumps(j, ensure_ascii=False), "Phản hồi chứa mã nghiên cứu")
    return f"HTTP 200; tuổi {j['ageAtVisit']}, HbA1c {j['hbA1c']}, HA {j['systolicBp']}/{j['diastolicBp']}; không mã, ngày, ghi chú"


@case("GRD-04", "Người chấm truy cập ảnh chưa chấm vòng 1 (ngoài bộ nghiên cứu)",
      "HTTP 403 cho cả ảnh và thông tin lâm sàng", needs=("dup", "roles_ready"))
def grd_04(c: Ctx):
    expect(c.call("grader1", "GET", f"/api/grading/images/{c.s['dup']}/content"), 403)
    expect(c.call("grader1", "GET", f"/api/grading/images/{c.s['dup']}/context"), 403)
    return "HTTP 403 / 403"


@case("GRD-05", "Người chấm 1 chấm vòng 2 ảnh OD: DR nặng (độ 3) — khác phía ngưỡng chuyển tuyến so với vòng 1 (độ 1)",
      "HTTP 200", needs=("od_r1", "roles_ready"))
def grd_05(c: Ctx):
    expect(c.call("grader1", "POST", f"/api/grading/round/2/{c.s['od']}", json={"gradable": True, "grade": 3}), 200, 201)
    c.s["od_r2"] = True
    return "Lưu vòng 2 độ 3"


@case("GRD-06", "Người chấm 1 chấm lại vòng 2 cùng ảnh", "HTTP 409", needs=("od_r2",))
def grd_06(c: Ctx):
    expect(c.call("grader1", "POST", f"/api/grading/round/2/{c.s['od']}", json={"gradable": True, "grade": 0}), 409)
    return "HTTP 409"


@case("GRD-07", "Hàng chờ vòng 3 của người chấm 1 (đã chấm vòng 2 ảnh này)",
      "KHÔNG có ảnh OD", needs=("od_r2",))
def grd_07(c: Ctx):
    q = _queue(c, "grader1", 3)
    check(c.s["od"] not in q, f"Người chấm vòng 2 thấy ảnh ở vòng 3: {q}")
    return f"Hàng chờ: {q}"


@case("GRD-08", "Hàng chờ vòng 3 của người chấm 2", "Có ảnh OD (vòng 1 và 2 bất đồng)", needs=("od_r2",))
def grd_08(c: Ctx):
    q = _queue(c, "grader2", 3)
    check(c.s["od"] in q, f"Thiếu OD trong hàng chờ vòng 3: {q}")
    return f"Hàng chờ: {q}"


@case("GRD-09", "Người chấm 2 phân xử vòng 3 ảnh OD: DR trung bình (độ 2)", "HTTP 200", needs=("od_r2",))
def grd_09(c: Ctx):
    expect(c.call("grader2", "POST", f"/api/grading/round/3/{c.s['od']}", json={"gradable": True, "grade": 2}), 200, 201)
    c.s["od_r3"] = True
    return "Lưu vòng 3 độ 2"


@case("GRD-10", "Tiến độ chấm mù toàn nghiên cứu",
      "round1=2, round1Ungradable=1, round2Done=1, round2Pending=0, round3Needed=1, round3Done=1", needs=("od_r3", "os_r1"))
def grd_10(c: Ctx):
    p = expect(c.call("research", "GET", "/api/grading/progress"), 200).json()
    want = {"round1": 2, "round1Ungradable": 1, "round2Done": 1, "round2Pending": 0, "round3Needed": 1, "round3Done": 1}
    got = {k: p.get(k) for k in want}
    check(got == want, f"Tiến độ: {got}")
    return ", ".join(f"{k}={v}" for k, v in got.items())

# ============================================================================ EXP

GRADING_COLS = ["image_id", "patient_code", "visit_id", "eye", "quality_status", "uploaded_at", "has_ai",
                "r1_gradable", "r1_grade", "r1_grader", "r1_at", "r2_gradable", "r2_grade", "r2_grader", "r2_at",
                "r3_gradable", "r3_grade", "r3_grader", "r3_at", "needs_adjudication", "reference_gradable",
                "reference_grade", "reference_referable", "r1_r2_disagree", "image_sha256",
                "r2_source", "r3_source"]
DATASET_NEW_COLS = ["reviewed_by_doctor", "ai_confidence", "ai_raw_score", "ai_fold_scores", "ai_fold_sd", "image_sha256"]


@case("EXP-01", "Tải bảng chấm mù grading.csv (tài khoản nghiên cứu)",
      "HTTP 200; UTF-8 có BOM; đủ 27 cột theo DATASET_COLUMNS.md; mỗi ảnh một dòng", needs=("od_r3", "roles_ready"))
def exp_01(c: Ctx):
    raw, cols, rows = read_csv(expect(c.call("research", "GET", "/api/research/grading.csv"), 200))
    check(raw.startswith(b"\xef\xbb\xbf"), "Thiếu BOM UTF-8")
    check(cols == GRADING_COLS, f"Cột khác mong đợi: {cols}")
    ids = {int(r["image_id"]) for r in rows}
    check({c.s["od"], c.s["os"], c.s["dup"]} <= ids, f"Thiếu dòng ảnh: {sorted(ids)}")
    c.s["grading"] = {int(r["image_id"]): r for r in rows}
    c.s["grading_raw"] = raw
    return f"{len(rows)} dòng, {len(cols)} cột, có BOM"


@case("EXP-02", "Dòng ảnh OD trong grading.csv",
      "r1=1, r2=3, r3=2; r1_r2_disagree=1; needs_adjudication=0; nhãn tham chiếu = 2, cần chuyển tuyến; mã người chấm G… khác nhau; "
      "r2_source = r3_source = app",
      needs=("grading",))
def exp_02(c: Ctx):
    r = c.s["grading"][c.s["od"]]
    got = (r["r1_grade"], r["r2_grade"], r["r3_grade"], r["r1_r2_disagree"], r["needs_adjudication"],
           r["reference_gradable"], r["reference_grade"], r["reference_referable"])
    check(got == ("1", "3", "2", "1", "0", "1", "2", "1"), f"Giá trị: {got}")
    graders = [r["r1_grader"], r["r2_grader"], r["r3_grader"]]
    check(all(re.fullmatch(r"G\d+", g) for g in graders) and len(set(graders)) == 3, f"Mã người chấm: {graders}")
    check(r["r2_source"] == "app" and r["r3_source"] == "app", f"Nguồn: {r['r2_source']}/{r['r3_source']}")
    return f"r1/r2/r3 = 1/3/2, tham chiếu 2 (chuyển tuyến), người chấm {'/'.join(graders)}"


@case("EXP-03", "Dòng ảnh OS (không chấm được ở vòng 1) và ảnh ngoài bộ chấm mù",
      "OS: r1_gradable=0, nhãn tham chiếu trống; ảnh trùng: không có vòng 1", needs=("grading",))
def exp_03(c: Ctx):
    o = c.s["grading"][c.s["os"]]
    check(o["r1_gradable"] == "0" and o["reference_grade"] == "" and o["quality_status"] == "2", f"OS: {o}")
    d = c.s["grading"][c.s["dup"]]
    check(d["r1_gradable"] == "" and d["reference_grade"] == "", f"Ảnh trùng: {d}")
    return "OS: r1_gradable=0, tham chiếu trống; ảnh trùng: chưa có vòng 1"


@case("EXP-04", "Mã băm ảnh image_sha256",
      "Khớp SHA-256 tệp đã nạp; ảnh nạp trùng ở hồ sơ thứ hai có CÙNG mã với ảnh OD", needs=("grading",))
def exp_04(c: Ctx):
    g = c.s["grading"]
    check(g[c.s["od"]]["image_sha256"] == c.s["od_sha"], "SHA-256 OD không khớp tệp gốc")
    check(g[c.s["os"]]["image_sha256"] == c.s["os_sha"], "SHA-256 OS không khớp tệp gốc")
    check(g[c.s["dup"]]["image_sha256"] == c.s["od_sha"], "Không phát hiện được ảnh trùng")
    return f"OD = ảnh trùng = {c.s['od_sha'][:12]}…; OS khớp"


@case("EXP-05", "Tải dataset.csv và kiểm tra dòng AI của ảnh OD",
      "Có các cột mới; ai_raw_score là số và = trung bình ai_fold_scores; ai_fold_sd đúng; ai_grade khớp ngưỡng của điểm; "
      "reviewed_by_doctor dạng D…; review_action = 1; doctor_grade = độ ghi đè; visit_hba1c = 8.2",
      needs=("reviewed", "roles_ready"))
def exp_05(c: Ctx):
    raw, cols, rows = read_csv(expect(c.call("research", "GET", "/api/research/dataset.csv"), 200))
    check(raw.startswith(b"\xef\xbb\xbf"), "Thiếu BOM UTF-8")
    missing = [x for x in DATASET_NEW_COLS if x not in cols]
    check(not missing and "ai_grade_probabilities" not in cols, f"Cột thiếu: {missing}")
    mine = [r for r in rows if r["image_id"] == str(c.s["od"])]
    check(len(mine) == 1, f"Số dòng của ảnh OD: {len(mine)}")
    r = mine[0]
    c.s["dataset_raw"] = raw
    score = float(r["ai_raw_score"])
    folds = [float(x) for x in r["ai_fold_scores"].split(";") if x]
    check(len(folds) >= 1, "ai_fold_scores trống")
    check(abs(statistics.mean(folds) - score) <= 0.0006, f"Trung bình fold {statistics.mean(folds):.4f} ≠ điểm {score}")
    check(abs(statistics.pstdev(folds) - float(r["ai_fold_sd"])) <= 0.0002, f"ai_fold_sd {r['ai_fold_sd']} sai")
    implied = sum(score >= t for t in DR_THRESHOLDS)
    check(int(r["ai_grade"]) == implied, f"ai_grade {r['ai_grade']} không khớp điểm {score} (ngưỡng → {implied})")
    check(re.fullmatch(r"D\d+", r["reviewed_by_doctor"] or "") is not None, f"reviewed_by_doctor = {r['reviewed_by_doctor']}")
    check(r["review_action"] == "1" and r["doctor_grade"] == str(c.s["override_grade"]),
          f"review_action={r['review_action']}, doctor_grade={r['doctor_grade']}")
    check(float(r["visit_hba1c"]) == 8.2, f"visit_hba1c = {r['visit_hba1c']}")
    check(r["image_sha256"] == c.s["od_sha"], "image_sha256 khác tệp gốc")
    check(r["ai_confidence"] != "" and 0 <= float(r["ai_confidence"]) <= 1, f"ai_confidence = {r['ai_confidence']}")
    return (f"điểm {score} (độ {implied}), {len(folds)} fold, SD {r['ai_fold_sd']}, "
            f"{r['reviewed_by_doctor']}, ghi đè → {r['doctor_grade']}")


@case("EXP-06", "Ẩn danh trong tệp kết xuất",
      "Cả hai CSV không chứa mã nghiên cứu, số điện thoại hay tên bác sĩ", needs=("grading_raw", "dataset_raw"))
def exp_06(c: Ctx):
    blob = (c.s["grading_raw"] + c.s["dataset_raw"]).decode("utf-8-sig")
    leaks = [x for x in (TEST_CODE, TEST_CODE_2, c.s.get("p1_phone", "§"), DOCTOR_NAME_MARK) if x and x in blob]
    check(not leaks, f"Lộ thông tin: {leaks}")
    return "Không tìm thấy mã nghiên cứu, SĐT, tên bác sĩ"

# ============================================================== PKG (1.3.1: chấm trên nhiều máy)
# Chạy SAU nhóm EXP để không làm đổi số liệu GRD-10 / EXP. Dùng bệnh nhân riêng NC-TEST-003:
# OD (JPG) vòng 1 độ 0, OS (PNG) vòng 1 độ 2. Gói vòng 2: OD độ 3 (khác phía ngưỡng → bất đồng), OS độ 2 (đồng thuận).
# Gói vòng 3: người chấm 2 phân xử OD.

TEST_CODE_3 = "NC-TEST-003"
PKG_FORMAT = "dr-ai-diagnosis/grading-result"


@case("IMG-06", "Hai yêu cầu nạp ảnh cùng mắt gửi ĐỒNG THỜI (mô phỏng bấm hai lần rất nhanh)",
      "Đúng một yêu cầu thành công, yêu cầu kia HTTP 409; lượt chỉ có một ảnh OD", needs=("doctor_id",))
def img_06(c: Ctx):
    p = _new_patient(c, TEST_CODE_3, 1971)
    v = expect(c.call("doctor", "POST", "/api/visits", json={"patientId": p["id"], "doctorId": c.s["doctor_id"]}), 200, 201).json()
    c.s["p3"], c.s["v3"] = p["id"], v["id"]
    data = c.fixture("fundus_od.jpg")
    headers = {"Authorization": f"Bearer {c.tokens['doctor']}"}
    out: list = [None, None]
    gate = threading.Barrier(2)

    def send(i: int):
        gate.wait()
        out[i] = requests.post(c.api + "/api/images", headers=headers, timeout=120,
                               files={"file": (f"od_{i}.jpg", data, "image/jpeg")},
                               data={"patientId": str(p["id"]), "visitId": str(v["id"]), "eye": "0"})

    threads = [threading.Thread(target=send, args=(i,)) for i in range(2)]
    for t in threads: t.start()
    for t in threads: t.join()
    codes = sorted(r.status_code for r in out)
    check(codes in ([200, 409], [201, 409]), f"Mã trả về: {codes}")
    ok = next(r for r in out if r.status_code in (200, 201))
    imgs = expect(c.call("doctor", "GET", f"/api/images?visitId={v['id']}"), 200).json()
    od = [x for x in imgs if x["eye"] == 0]
    check(len(od) == 1, f"Lượt có {len(od)} ảnh OD")
    c.s["od3"] = ok.json()["id"]
    return f"Mã trả về {codes}; lượt có 1 ảnh OD (Id={c.s['od3']})"


@case("PKG-00", "Chuẩn bị: nạp ảnh OS (PNG) cho NC-TEST-003; bác sĩ chấm mù vòng 1: OD độ 0, OS độ 2",
      "HTTP 200 cho cả ba thao tác", needs=("od3",))
def pkg_00(c: Ctx):
    img = expect(_upload(c, c.s["p3"], c.s["v3"], 1, "fundus_os.png", c.fixture("fundus_os.png"), "image/png"), 200, 201).json()
    c.s["os3"] = img["id"]
    expect(c.call("doctor", "POST", f"/api/grading/round1/{c.s['od3']}", json={"gradable": True, "grade": 0}), 200, 201)
    expect(c.call("doctor", "POST", f"/api/grading/round1/{c.s['os3']}", json={"gradable": True, "grade": 2}), 200, 201)
    c.s["pkg_ready"] = True
    return f"OD Id={c.s['od3']} (độ 0), OS Id={c.s['os3']} (độ 2)"


@case("AI-02", "Chạy AI cho HAI mắt cùng lúc (như bác sĩ chấm xong hai mắt liên tiếp, lần chạy AI đầu tiên sau khi cài)",
      "Cả hai HTTP 200; mỗi ảnh đúng MỘT kết quả AI còn hiệu lực (lỗi cũ: deadlock + tự thử lại ghi thêm bản thừa)",
      needs=("pkg_ready",))
def ai_02(c: Ctx):
    headers = {"Authorization": f"Bearer {c.tokens['doctor']}"}
    ids = [c.s["od3"], c.s["os3"]]
    out: list = [None, None]
    gate = threading.Barrier(2)

    def run(i: int):
        gate.wait()
        out[i] = requests.post(f"{c.api}/api/diagnoses/run/{ids[i]}", headers=headers, timeout=c.ai_timeout)

    threads = [threading.Thread(target=run, args=(i,)) for i in range(2)]
    for t in threads: t.start()
    for t in threads: t.join()
    for r in out:
        expect(r, 200, 201)
    counts = [len(expect(c.call("doctor", "GET", f"/api/diagnoses/by-image/{i}"), 200).json()) for i in ids]
    check(counts == [1, 1], f"Số kết quả AI còn hiệu lực mỗi ảnh: {counts}")
    return f"HTTP 200 / 200; mỗi ảnh 1 kết quả AI (Id {out[0].json()['id']}, {out[1].json()['id']})"


def _graders(c: Ctx) -> dict[int, dict]:
    return {g["id"]: g for g in expect(c.call("research", "GET", "/api/grading-packages/graders"), 200).json()}


def _create_pkg(c: Ctx, rnd: int, grader: str, **extra) -> requests.Response:
    return c.call("research", "POST", "/api/grading-packages",
                  json={"round": rnd, "graderId": c.s[f"{grader}_id"], "maxImages": 150, "note": "kiểm thử", **extra})


def _download_pkg(c: Ctx, pkg_id: int) -> zipfile.ZipFile:
    r = expect(c.call("research", "GET", f"/api/grading-packages/{pkg_id}/download", timeout=180), 200)
    return zipfile.ZipFile(io.BytesIO(r.content))


def _manifest(z: zipfile.ZipFile, code: str) -> dict:
    js = z.read(f"{code}/data.js").decode("utf-8")
    m = re.search(r"window\.DRAI_PACKAGE\s*=\s*(\{.*\});\s*$", js, re.S)
    check(m is not None, "data.js không đúng dạng window.DRAI_PACKAGE = {...};")
    return json.loads(m.group(1))


def _result_file(code: str, key: str, rnd: int, items: list[dict]) -> dict:
    """Dựng tệp kết quả giống trang chấm xuất ra, cùng cách tính mã kiểm tra (GradingResultChecksum.cs)."""
    s = f"{code}|{key}|{rnd}\n"
    for x in items:
        s += (f"{x['t']}|{1 if x['gradable'] else 0}|{'' if x.get('grade') is None else x['grade']}|"
              f"{x.get('note') or ''}|{x.get('at') or ''}\n")
    return {"format": PKG_FORMAT, "formatVersion": 1, "package": code, "key": key, "round": rnd,
            "exportedAt": time.strftime("%Y-%m-%dT%H:%M:%S+07:00"), "items": items,
            "checksum": hashlib.sha256(s.encode("utf-8")).hexdigest()}


def _import(c: Ctx, doc: dict, who: str = "research") -> requests.Response:
    body = json.dumps(doc, ensure_ascii=False).encode("utf-8")
    return c.call(who, "POST", "/api/grading-packages/import", timeout=120,
                  files={"file": (f"KET-QUA-{doc.get('package')}.json", body, "application/json")})


@case("PKG-01", "Danh sách người chấm cho gói (tài khoản nghiên cứu)",
      "Có người chấm 1 và 2; mỗi người còn đúng 2 ảnh vòng 2 (OD, OS của NC-TEST-003)", needs=("pkg_ready", "roles_ready"))
def pkg_01(c: Ctx):
    g = _graders(c)
    g1, g2 = g.get(c.s["grader1_id"]), g.get(c.s["grader2_id"])
    check(g1 is not None and g2 is not None, f"Thiếu người chấm trong danh sách: {sorted(g)}")
    check(g1["round2Available"] == 2 and g2["round2Available"] == 2,
          f"Ảnh vòng 2 còn chấm được: G1={g1['round2Available']}, G2={g2['round2Available']}")
    return f"{len(g)} người chấm; G1 còn {g1['round2Available']}, G2 còn {g2['round2Available']} ảnh vòng 2"


@case("PKG-02", "Tạo gói vòng 2 cho người chấm 2 rồi hủy gói",
      "Tạo: 2 ảnh, trạng thái mở; hủy: trạng thái 2; tải gói đã hủy: HTTP 409; hủy lần nữa: HTTP 409; "
      "ảnh trở lại hàng chờ vòng 2 của người chấm 2", needs=("pkg_ready", "roles_ready"))
def pkg_02(c: Ctx):
    p = expect(_create_pkg(c, 2, "grader2"), 200, 201).json()
    check(p["itemCount"] == 2 and p["status"] == 0, f"Gói: {p}")
    q = _queue(c, "grader2", 2)
    check(c.s["od3"] not in q and c.s["os3"] not in q, f"Ảnh trong gói vẫn ở hàng chờ trong ứng dụng: {q}")
    x = expect(c.call("research", "POST", f"/api/grading-packages/{p['id']}/cancel"), 200).json()
    check(x["status"] == 2, f"Trạng thái sau hủy: {x['status']}")
    expect(c.call("research", "GET", f"/api/grading-packages/{p['id']}/download"), 409)
    expect(c.call("research", "POST", f"/api/grading-packages/{p['id']}/cancel"), 409)
    q = _queue(c, "grader2", 2)
    check(c.s["od3"] in q and c.s["os3"] in q, f"Ảnh chưa trở lại hàng chờ: {q}")
    c.s["pkg_cancelled"] = p["code"]
    return f"Gói {p['code']} tạo rồi hủy; ảnh trở lại hàng chờ"


@case("PKG-03", "Tạo gói vòng 2 cho người chấm 1 (máy 2)",
      "HTTP 200; mã V2-yyyyMMdd-xxxx; 2 ảnh; ảnh trong gói không còn ở hàng chờ vòng 2 trong ứng dụng; "
      "tiến độ round2InPackages = 2", needs=("pkg_ready", "roles_ready"))
def pkg_03(c: Ctx):
    p = expect(_create_pkg(c, 2, "grader1"), 200, 201).json()
    check(re.fullmatch(r"V2-\d{8}-[A-Z0-9]{4}", p["code"]) is not None, f"Mã gói: {p['code']}")
    check(p["itemCount"] == 2 and p["status"] == 0 and p["graderId"] == c.s["grader1_id"], f"Gói: {p}")
    for who in ("grader1", "grader2"):
        q = _queue(c, who, 2)
        check(c.s["od3"] not in q and c.s["os3"] not in q, f"Hàng chờ của {who} còn ảnh trong gói: {q}")
    prog = expect(c.call("research", "GET", "/api/grading/progress"), 200).json()
    check(prog.get("round2InPackages") == 2, f"round2InPackages = {prog.get('round2InPackages')}")
    c.s["pkg2"] = p
    return f"Gói {p['code']}, 2 ảnh; round2InPackages=2"


@case("PKG-04", "Tạo thêm gói vòng 2 khi mọi ảnh đã nằm trong gói đang mở",
      "HTTP 400; người chấm 2 còn 0 ảnh vòng 2", needs=("pkg2",))
def pkg_04(c: Ctx):
    r = expect(_create_pkg(c, 2, "grader2"), 400)
    g2 = _graders(c)[c.s["grader2_id"]]
    check(g2["round2Available"] == 0, f"G2 còn {g2['round2Available']} ảnh vòng 2")
    return "HTTP 400: " + msg_of(r)


@case("PKG-05", "Tải gói vòng 2 (zip) và kiểm tra nội dung",
      "Có index.html, data.js, HUONG-DAN.txt và 2 ảnh đặt tên ngẫu nhiên; mỗi ảnh có thông tin lâm sàng ẩn danh; "
      "không có mã nghiên cứu, ghi chú, kết quả AI, phân độ vòng 1; ảnh JPEG/PNG hợp lệ, không còn EXIF/tEXt", needs=("pkg2",))
def pkg_05(c: Ctx):
    code = c.s["pkg2"]["code"]
    z = _download_pkg(c, c.s["pkg2"]["id"])
    names = z.namelist()
    for f in ("index.html", "data.js", "HUONG-DAN.txt"):
        check(f"{code}/{f}" in names, f"Thiếu {f}: {names}")
    m = _manifest(z, code)
    check(m.get("package") == code and m.get("round") == 2 and len(m.get("items", [])) == 2, f"data.js: {m}")
    check(set(m) <= {"format", "formatVersion", "app", "package", "key", "round", "graderName", "createdAt", "items"},
          f"data.js có trường lạ: {sorted(m)}")
    blob = b"".join(z.read(n) for n in names if not n.startswith(f"{code}/images/")).decode("utf-8", "replace")
    # Trang chấm (index.html) là mẫu cố định, không chứa dữ liệu: tìm trường dữ liệu trong data.js và HUONG-DAN.txt.
    data_txt = (z.read(f"{code}/data.js") + z.read(f"{code}/HUONG-DAN.txt")).decode("utf-8")
    leaks = [w for w in (TEST_CODE_3, "Hồ sơ kiểm thử tự động", "researchCode", "birthYear", "visitDate",
                         "aiGrade", "confidence", "\"grade\"") if w in data_txt]
    check(not leaks, f"Gói lộ thông tin: {leaks}")
    check(TEST_CODE_3 not in blob, "Mã nghiên cứu xuất hiện trong gói")
    by_ext = {}
    for it in m["items"]:
        cx = it.get("c") or {}
        check(cx.get("age") and cx.get("diabetesType") == 2 and cx.get("diabetesYears") == 12,
              f"Thiếu thông tin lâm sàng ẩn danh: {cx}")
        check(re.fullmatch(r"[A-HJKMNP-Z2-9]{10}", it["t"]) is not None, f"Mã ảnh không ngẫu nhiên: {it['t']}")
        raw = z.read(f"{code}/{it['f']}")
        if it["f"].endswith(".jpg"):
            check(raw[:3] == b"\xff\xd8\xff", f"{it['f']} không phải JPEG")
            check(b"Exif\x00\x00" not in raw, f"{it['f']} còn EXIF")
        elif it["f"].endswith(".png"):
            check(raw[:8] == b"\x89PNG\r\n\x1a\n", f"{it['f']} không phải PNG")
            check(b"tEXt" not in raw and b"eXIf" not in raw and b"tIME" not in raw, f"{it['f']} còn metadata PNG")
        else:
            raise AssertionError(f"Phần mở rộng lạ: {it['f']}")
        by_ext[it["f"].rsplit(".", 1)[1]] = it["t"]
    check(set(by_ext) == {"jpg", "png"}, f"Ảnh trong gói: {by_ext}")
    check(all(n.startswith(f"{code}/") for n in names), f"Có mục nằm ngoài thư mục {code}/: {names}")
    # OD nạp bằng JPG, OS bằng PNG: suy ra mã ảnh của từng mắt để dựng tệp kết quả.
    c.s["pkg2_key"] = m["key"]
    c.s["pkg2_tok"] = {"od": by_ext["jpg"], "os": by_ext["png"]}
    return f"{len(names)} mục trong zip; mã ảnh {by_ext['jpg']} (jpg), {by_ext['png']} (png); không lộ thông tin"


@case("PKG-06", "Nhập tệp kết quả vòng 2 (OD độ 3, OS độ 2) như máy 2 xuất ra",
      "HTTP 200; nhập mới 2; gói chuyển sang Đã nhập đủ (status 1, còn 0)", needs=("pkg2_tok",))
def pkg_06(c: Ctx):
    code, key, tok = c.s["pkg2"]["code"], c.s["pkg2_key"], c.s["pkg2_tok"]
    at = time.strftime("%Y-%m-%dT%H:%M:%S+07:00")
    doc = _result_file(code, key, 2, [
        {"t": tok["od"], "gradable": True, "grade": 3, "note": "máy 2", "at": at},
        {"t": tok["os"], "gradable": True, "grade": 2, "note": None, "at": at},
    ])
    r = expect(_import(c, doc), 200).json()
    check(r["imported"] == 2 and r["unchanged"] == 0 and not r["conflicts"] and not r["invalid"], f"Kết quả: {r}")
    check(r["packageStatus"] == 1 and r["remaining"] == 0, f"Trạng thái gói: {r['packageStatus']}, còn {r['remaining']}")
    c.s["pkg2_doc"] = doc
    return f"Nhập mới {r['imported']}; gói đã nhập đủ"


@case("PKG-07", "Nhập lại đúng tệp kết quả vòng 2", "HTTP 200; nhập mới 0, đã có từ trước 2 (không ghi trùng)",
      needs=("pkg2_doc",))
def pkg_07(c: Ctx):
    r = expect(_import(c, c.s["pkg2_doc"]), 200).json()
    check(r["imported"] == 0 and r["unchanged"] == 2 and not r["conflicts"], f"Kết quả: {r}")
    return "Nhập mới 0, đã có 2"


@case("PKG-08", "Nhập tệp kết quả bị sửa tay (đổi phân độ, giữ mã kiểm tra cũ) và tệp sai khóa gói",
      "HTTP 400 cho cả hai; không ghi gì", needs=("pkg2_doc",))
def pkg_08(c: Ctx):
    bad = json.loads(json.dumps(c.s["pkg2_doc"]))
    bad["items"][1]["grade"] = 4
    r1 = expect(_import(c, bad), 400)
    wrong = _result_file(bad["package"], "0" * 48, 2, c.s["pkg2_doc"]["items"])
    r2 = expect(_import(c, wrong), 400)
    return f"{msg_of(r1)} / {msg_of(r2)}"


@case("PKG-09", "Người chấm và bác sĩ gọi API gói chấm",
      "HTTP 403 (chỉ tài khoản nghiên cứu tạo/nhập gói)", needs=("pkg2_doc", "roles_ready"))
def pkg_09(c: Ctx):
    expect(c.call("grader1", "GET", "/api/grading-packages"), 403)
    expect(c.call("doctor", "GET", "/api/grading-packages"), 403)
    expect(_import(c, c.s["pkg2_doc"], who="grader1"), 403)
    return "HTTP 403 / 403 / 403"


@case("PKG-10", "Gói vòng 3 (máy 3 phân xử) cho ảnh OD bất đồng",
      "Người chấm 1 (đã chấm vòng 2): 0 ảnh, tạo gói HTTP 400; người chấm 2: gói 1 ảnh (OD), "
      "nhập kết quả độ 1: nhập mới 1, gói đã nhập đủ", needs=("pkg2_doc",))
def pkg_10(c: Ctx):
    g = _graders(c)
    check(g[c.s["grader1_id"]]["round3Available"] == 0, f"G1 vòng 3: {g[c.s['grader1_id']]['round3Available']}")
    check(g[c.s["grader2_id"]]["round3Available"] == 1, f"G2 vòng 3: {g[c.s['grader2_id']]['round3Available']}")
    expect(_create_pkg(c, 3, "grader1"), 400)
    p = expect(_create_pkg(c, 3, "grader2"), 200, 201).json()
    check(re.fullmatch(r"V3-\d{8}-[A-Z0-9]{4}", p["code"]) is not None and p["itemCount"] == 1, f"Gói: {p}")
    z = _download_pkg(c, p["id"])
    m = _manifest(z, p["code"])
    check(len(m["items"]) == 1 and m["items"][0]["f"].endswith(".jpg"), f"Ảnh trong gói vòng 3: {m['items']}")
    doc = _result_file(p["code"], m["key"], 3, [
        {"t": m["items"][0]["t"], "gradable": True, "grade": 1, "note": "phân xử", "at": None}])
    r = expect(_import(c, doc), 200).json()
    check(r["imported"] == 1 and r["packageStatus"] == 1, f"Kết quả: {r}")
    c.s["pkg3"] = p
    return f"Gói {p['code']}: 1 ảnh OD, nhập mới 1"


@case("PKG-11", "grading.csv sau khi nhập gói",
      "OD NC-TEST-003: r1=0, r2=3 (pkg:V2-…), r3=1 (pkg:V3-…), bất đồng, tham chiếu = 1; "
      "OS: r2=2 (pkg:V2-…), r3 trống, không bất đồng", needs=("pkg3",))
def pkg_11(c: Ctx):
    _, cols, rows = read_csv(expect(c.call("research", "GET", "/api/research/grading.csv"), 200))
    check(cols == GRADING_COLS, f"Cột: {cols}")
    by = {int(r["image_id"]): r for r in rows}
    od, os_ = by[c.s["od3"]], by[c.s["os3"]]
    v2, v3 = "pkg:" + c.s["pkg2"]["code"], "pkg:" + c.s["pkg3"]["code"]
    got = (od["r1_grade"], od["r2_grade"], od["r3_grade"], od["r2_source"], od["r3_source"],
           od["r1_r2_disagree"], od["needs_adjudication"], od["reference_grade"])
    check(got == ("0", "3", "1", v2, v3, "1", "0", "1"), f"Dòng OD: {got}")
    check(od["r2_grader"] == f"G{c.s['grader1_id']}" and od["r3_grader"] == f"G{c.s['grader2_id']}",
          f"Người chấm: {od['r2_grader']}/{od['r3_grader']}")
    got = (os_["r2_grade"], os_["r2_source"], os_["r3_grade"], os_["r3_source"], os_["r1_r2_disagree"])
    check(got == ("2", v2, "", "", "0"), f"Dòng OS: {got}")
    return f"OD {v2} / {v3}; OS {v2}"
