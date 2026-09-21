#!/usr/bin/env python3
"""
Sinh script SQL khởi tạo cho bản DiaCompanion "Thu thập dữ liệu" (Lite).

  init            Tạo 4 role (nếu thiếu), 1 tài khoản nghiên cứu có đủ hai vai trò
                  Doctor + Receptionist (mật khẩu tạm, buộc đổi ở lần đăng nhập
                  đầu) và 3 phiên bản model AI đang active (DR / Lesion / Fractal).
  reset-password  Sinh câu UPDATE cấp lại mật khẩu tạm cho tài khoản đã có.

Script CHỈ SINH FILE .sql; không kết nối CSDL. Chạy file đó bằng SSMS hoặc
    sqlcmd -S <server> -d DiaCompanion -i lite_seed.sql
Mật khẩu tạm chỉ in ra MỘT lần trên màn hình — hãy ghi lại ngay. Định dạng băm
khớp PasswordHasher.cs: PBKDF2$100000$<salt b64>$<hash b64> (HMAC-SHA256, 32 byte).
Toàn bộ SQL sinh ra chạy lại nhiều lần không sao (idempotent).
"""
import argparse, base64, glob, hashlib, os, re, secrets, string, sys

ITER, SALT, KEY = 100_000, 16, 32
ZERO = "0" * 64


def pbkdf2(password: str) -> str:
    salt = os.urandom(SALT)
    key = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, ITER, KEY)
    b64 = lambda b: base64.b64encode(b).decode("ascii")
    return f"PBKDF2${ITER}${b64(salt)}${b64(key)}"


def verify(password: str, stored: str) -> bool:  # dùng để tự kiểm tra, giống PasswordHasher.Verify
    p = stored.split("$")
    if len(p) != 4 or p[0] != "PBKDF2":
        return False
    salt, exp = base64.b64decode(p[2]), base64.b64decode(p[3])
    act = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, int(p[1]), len(exp))
    return secrets.compare_digest(act, exp)


def temp_password() -> str:
    """12 ký tự, chắc chắn có cả chữ và số (PasswordHasher.EnsureStrong yêu cầu ≥ 8, chữ + số)."""
    letters = "".join(c for c in string.ascii_letters if c not in "lIO")
    digits = "23456789"
    while True:
        pw = "".join(secrets.choice(letters + digits) for _ in range(12))
        if any(c in letters for c in pw) and any(c in digits for c in pw):
            return pw


def sha_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def sha_folds(folder: str) -> str:
    """model_1 là ensemble 5 fold: băm nối các tệp fold theo thứ tự tên."""
    files = sorted(glob.glob(os.path.join(folder, "efficientnet_b4_fold*_best.pth")))
    if not files:
        raise FileNotFoundError(folder)
    h = hashlib.sha256()
    for p in files:
        with open(p, "rb") as f:
            for chunk in iter(lambda: f.read(1 << 20), b""):
                h.update(chunk)
    return h.hexdigest()


def q(s: str) -> str:
    return "N'" + s.replace("'", "''") + "'"


def models(ai_dir: str, allow_missing: bool):
    spec = [
        (1, "dr-efficientnet-b4-v4", "models/model_1/weights", "5-fold EfficientNet-B4 ordinal regression", "0.8378", "NULL", "NULL",
         lambda: sha_folds(os.path.join(ai_dir, "models/model_1/weights"))),
        (2, "lesion-unet-effb2-v4.2", "models/model_2/weights/tjdr_unet_v4_2_best.pth", "U-Net EfficientNet-B2, TJDR", "NULL", "0.521", "0.370",
         lambda: sha_file(os.path.join(ai_dir, "models/model_2/weights/tjdr_unet_v4_2_best.pth"))),
        (3, "fractal-vessel-keras", "models/model_3/weights/best_model.keras", "Vessel U-Net (Keras) + box-counting FD", "NULL", "NULL", "NULL",
         lambda: sha_file(os.path.join(ai_dir, "models/model_3/weights/best_model.keras"))),
    ]
    out = []
    for t, name, rel, note, qwk, dice, iou, hasher in spec:
        try:
            sha = hasher()
        except (FileNotFoundError, OSError):
            if not allow_missing:
                sys.exit(f"Không tìm thấy trọng số: {rel} (trong {ai_dir}). Đặt trọng số đúng chỗ, "
                         f"hoặc thêm --allow-missing-weights chỉ để thử.")
            print(f"[CẢNH BÁO] thiếu {rel} → dùng SHA-256 giả toàn số 0", file=sys.stderr)
            sha = ZERO
        out.append((t, name, rel, sha, note, qwk, dice, iou))
    return out


ROLE_RESEARCH = ("Research", "Nghiên cứu viên", "Chỉ kết xuất dữ liệu nghiên cứu (Gap 2); không truy cập lâm sàng")


def research_sql(email: str) -> tuple[str, str]:
    """SQL idempotent: role Research (Id 4) + tài khoản chỉ-kết-xuất. Trả (sql, mật khẩu tạm)."""
    rpw = temp_password()
    rh = pbkdf2(rpw)
    assert verify(rpw, rh)
    name, disp, desc = ROLE_RESEARCH
    sql = f"""
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = '{name}')
    INSERT INTO dbo.Roles (Id, Name, DisplayName, Description, IsActive, CreatedAt) VALUES (4, '{name}', {q(disp)}, {q(desc)}, 1, SYSUTCDATETIME());
DECLARE @remail nvarchar(256) = {q(email.strip())};
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @remail)
    INSERT INTO dbo.Users (PublicId, Email, PasswordHash, FullName, MustChangePassword, CreatedAt, IsVoided)
    VALUES (NEWID(), @remail, {q(rh)}, N'Nghiên cứu viên', 1, SYSUTCDATETIME(), 0);
DECLARE @ruid int = (SELECT Id FROM dbo.Users WHERE Email = @remail);
INSERT INTO dbo.UserRoles (UserId, RoleId, AssignedAt, AssignedBy, IsActive)
SELECT @ruid, r.Id, SYSUTCDATETIME(), @ruid, 1
FROM dbo.Roles r
WHERE r.Name = 'Research'
  AND NOT EXISTS (SELECT 1 FROM dbo.UserRoles ur WHERE ur.UserId = @ruid AND ur.RoleId = r.Id);
UPDATE ur SET IsActive = 1
FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.Id = ur.RoleId
WHERE ur.UserId = @ruid AND r.Name = 'Research';
"""
    return sql, rpw


def add_research(a) -> tuple[str, str, str | None]:
    sql, pw = research_sql(a.email)
    return "SET NOCOUNT ON; SET XACT_ABORT ON;\nBEGIN TRAN;\n" + sql + "\nCOMMIT TRAN;\n", pw, None


def build_init(a) -> tuple[str, str, str | None]:
    pw = temp_password()
    h = pbkdf2(pw)
    assert verify(pw, h)
    email = a.email.strip()
    s = []
    s.append("/* DiaCompanion Lite — khởi tạo dữ liệu nền. Idempotent. Sinh bởi seed_lite.py */")
    s.append("SET NOCOUNT ON; SET XACT_ABORT ON;\nBEGIN TRAN;\n")
    for rid, name, disp, desc in [(0, "Admin", "Quản trị viên", "Quản trị và cấu hình toàn bộ hệ thống"),
                                  (1, "Doctor", "Bác sĩ", "Khám bệnh, chẩn đoán và kết luận hồ sơ"),
                                  (2, "Receptionist", "Lễ tân", "Tiếp nhận bệnh nhân và tạo lượt khám"),
                                  (3, "Patient", "Bệnh nhân", "Sử dụng ứng dụng dành cho bệnh nhân"),
                                  (4, "Research", "Nghiên cứu viên", "Chỉ kết xuất dữ liệu nghiên cứu (Gap 2); không truy cập lâm sàng")]:
        s.append(f"IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = '{name}')\n"
             f"    INSERT INTO dbo.Roles (Id, Name, DisplayName, Description, IsActive, CreatedAt) VALUES ({rid}, '{name}', {q(disp)}, {q(desc)}, 1, SYSUTCDATETIME());")
    s.append(f"""
DECLARE @email nvarchar(256) = {q(email)};
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @email)
    INSERT INTO dbo.Users (PublicId, Email, PasswordHash, FullName, LicenseNo, MustChangePassword, CreatedAt, IsVoided)
    VALUES (NEWID(), @email, {q(h)}, {q(a.full_name)}, {q(a.license)}, 1, SYSUTCDATETIME(), 0);
DECLARE @uid int = (SELECT Id FROM dbo.Users WHERE Email = @email);

INSERT INTO dbo.UserRoles (UserId, RoleId, AssignedAt, AssignedBy, IsActive)
SELECT @uid, r.Id, SYSUTCDATETIME(), @uid, 1
FROM dbo.Roles r
WHERE r.Name IN ('Doctor', 'Receptionist')
  AND NOT EXISTS (SELECT 1 FROM dbo.UserRoles ur WHERE ur.UserId = @uid AND ur.RoleId = r.Id);
UPDATE ur SET IsActive = 1
FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.Id = ur.RoleId
WHERE ur.UserId = @uid AND r.Name IN ('Doctor', 'Receptionist');
""")
    research_pw = None
    if a.research_email:
        rsql, research_pw = research_sql(a.research_email)
        s.append(rsql)
    for t, name, rel, sha, note, qwk, dice, iou in models(a.ai_service_dir, a.allow_missing_weights):
        s.append(f"""IF NOT EXISTS (SELECT 1 FROM dbo.ModelVersions WHERE ModelType = {t} AND IsActive = 1)
    INSERT INTO dbo.ModelVersions (ModelType, Name, FilePath, Sha256, Qwk, Dice, IoU, Note, IsActive, WasActivated, ActivatedAt, CreatedBy, CreatedAt)
    VALUES ({t}, {q(name)}, {q(rel)}, '{sha}', {qwk}, {dice}, {iou}, {q(note)}, 1, 1, SYSUTCDATETIME(), @uid, SYSUTCDATETIME());""")
    s.append("\nCOMMIT TRAN;\nSELECT u.Id, u.Email, u.FullName, u.MustChangePassword FROM dbo.Users u WHERE u.Email = @email;\n"
             "SELECT ModelType, Name, FilePath, IsActive FROM dbo.ModelVersions WHERE IsActive = 1 ORDER BY ModelType;")
    return "\n".join(s) + "\n", pw, research_pw


def build_reset(a) -> tuple[str, str, str | None]:
    pw = temp_password()
    h = pbkdf2(pw)
    assert verify(pw, h)
    sql = (f"UPDATE dbo.Users SET PasswordHash = {q(h)}, MustChangePassword = 1, UpdatedAt = SYSUTCDATETIME()\n"
           f"WHERE Email = {q(a.email.strip())} AND IsVoided = 0;\nSELECT @@ROWCOUNT AS RowsUpdated;\n")
    return sql, pw, None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    i = sub.add_parser("init")
    i.add_argument("--email", required=True, help="tài khoản bác sĩ (đủ hai vai trò Doctor + Receptionist)")
    i.add_argument("--research-email", help="tài khoản chỉ-kết-xuất (vai trò Research); bỏ trống để không tạo")
    i.add_argument("--full-name", default="Bác sĩ")
    i.add_argument("--license", default="NCKH-001", help="số chứng chỉ hành nghề (BR-10: Doctor bắt buộc có)")
    i.add_argument("--ai-service-dir", default="../ai_service")
    i.add_argument("--allow-missing-weights", action="store_true", help="chỉ để thử: bỏ qua việc thiếu trọng số")
    i.add_argument("--out", default="lite_seed.sql")
    i.add_argument("--password-out", help="ghi mật khẩu tạm vào tệp này (quyền 600) thay vì chỉ in ra màn hình")
    ar = sub.add_parser("add-research", help="thêm role + tài khoản Research vào DB đã seed từ trước")
    ar.add_argument("--email", required=True)
    ar.add_argument("--out", default="lite_add_research.sql")
    ar.add_argument("--password-out", help="ghi mật khẩu tạm vào tệp này (quyền 600)")
    r = sub.add_parser("reset-password")
    r.add_argument("--email", required=True)
    r.add_argument("--out", default="lite_reset_password.sql")
    r.add_argument("--password-out", help="ghi mật khẩu tạm vào tệp này (quyền 600)")
    a = ap.parse_args()
    if not re.fullmatch(r"[^@\s]+@[^@\s]+", a.email):
        sys.exit("Email không hợp lệ.")
    builders = {"init": build_init, "reset-password": build_reset, "add-research": add_research}
    sql, pw, research_pw = builders[a.cmd](a)
    with open(a.out, "w", encoding="utf-8-sig") as f:  # BOM để SSMS/sqlcmd đọc đúng tiếng Việt
        f.write(sql)
    if a.password_out:
        with open(a.password_out, "w", encoding="utf-8") as f:
            who = "Nghiên cứu" if a.cmd == "add-research" else "Bác sĩ"
            f.write(f"{who} — Email: {a.email}\n         Mật khẩu tạm: {pw}\n")
            if getattr(a, "research_email", None) and research_pw:
                f.write(f"Nghiên cứu — Email: {a.research_email}\n           Mật khẩu tạm: {research_pw}\n")
            f.write("(Đổi mật khẩu ngay lần đăng nhập đầu, rồi xóa tệp này.)\n")
        os.chmod(a.password_out, 0o600)
    print(f"Đã ghi {a.out}")
    print(f"Email: {a.email}\nMẬT KHẨU TẠM (chỉ hiện một lần): {pw}")


if __name__ == "__main__":
    main()
