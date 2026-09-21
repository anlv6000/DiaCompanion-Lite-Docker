import { useState, type FormEvent, type ReactElement } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";
import { hasRole } from "@/lib/roles";
import { Button, Field } from "@/components/ui";
import { LiteShell } from "@/lite/LiteShell";
import type { LoginResponse } from "@/types/api";

/** Trang mặc định sau đăng nhập theo vai trò: nghiên cứu → kết xuất, còn lại → danh sách ca. */
export function liteLanding(user: Pick<LoginResponse, "role" | "roles">): string {
  const research = hasRole(user, "Research");
  const clinical = hasRole(user, "Doctor") && hasRole(user, "Receptionist");
  if (research && !clinical) return "/export";
  return "/cases";
}

/* Đăng nhập nhân viên. Không có "Quên mật khẩu" tự phục vụ: người quản trị hệ
   thống cấp lại mật khẩu (script SQL đi kèm gói Lite). */
export function LiteLoginPage() {
  const { login } = useAuth();
  const [loginId, setLoginId] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      await login(loginId.trim(), password);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login">
      <div className="login-card">
        <div className="login-side">
          <h1>DiaCompanion</h1>
          <p>Công cụ thu thập dữ liệu sàng lọc bệnh võng mạc đái tháo đường.</p>
          <div className="stack">
            <span>• Tạo hồ sơ, nhập chỉ số, nạp ảnh đáy mắt hai mắt.</span>
            <span>• AI chỉ hỗ trợ; bác sĩ xác nhận kết quả.</span>
          </div>
        </div>
        <form className="login-form" onSubmit={submit}>
          <h2 className="serif">Đăng nhập</h2>
          <p className="faint">Tài khoản do quản trị hệ thống cấp.</p>
          <Field labelText="Email" required>
            <input
              autoFocus
              type="text"
              value={loginId}
              onChange={(e) => setLoginId(e.target.value)}
              placeholder="Email đăng nhập"
              autoComplete="username"
            />
          </Field>
          <Field labelText="Mật khẩu" required>
            <div className="input-with-action">
              <input
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
              />
              <Button type="button" onClick={() => setShowPassword((x) => !x)}>
                {showPassword ? "Ẩn" : "Hiện"}
              </Button>
            </div>
          </Field>
          {error && <div className="state error">{error}</div>}
          <Button
            kind="primary"
            type="submit"
            busy={busy}
            disabled={!loginId.trim() || !password}
            style={{ width: "100%", justifyContent: "center", marginTop: 8 }}
          >
            Đăng nhập
          </Button>
        </form>
      </div>
    </div>
  );
}

/* Cổng bảo vệ. Hai loại người dùng của bản thu thập dữ liệu:
     - Lâm sàng: cần ĐỦ hai vai trò Doctor + Receptionist (bác sĩ làm tất cả:
       tạo hồ sơ/lượt, nạp ảnh, chạy AI, xác nhận, xem diễn tiến).
     - Nghiên cứu: vai trò Research, CHỈ được vào trang kết xuất dữ liệu.
   `need` khai báo quyền tối thiểu cho nhánh route. Backend vẫn là chốt chặn thật. */
export function LiteGate({
  children,
  need = "clinical",
}: {
  children: ReactElement;
  need?: "clinical" | "research" | "any";
}) {
  const { user } = useAuth();
  const location = useLocation();
  if (!user) return <Navigate to="/login" replace state={{ from: location }} />;
  if (user.mustChangePassword && location.pathname !== "/change-password") {
    return <Navigate to="/change-password" replace />;
  }

  const clinical = hasRole(user, "Doctor") && hasRole(user, "Receptionist");
  const research = hasRole(user, "Research");
  const ok =
    need === "any"
      ? clinical || research
      : need === "research"
        ? research || hasRole(user, "Admin")
        : clinical;

  return (
    <LiteShell>
      {ok ? (
        children
      ) : (
        <div className="state error" style={{ margin: 24 }}>
          <b>Tài khoản không có quyền cho khu vực này</b>
          <div>
            {need === "research"
              ? "Cần vai trò Nghiên cứu (Research)."
              : "Cần đồng thời hai vai trò Doctor và Receptionist."}{" "}
            Liên hệ quản trị hệ thống.
          </div>
        </div>
      )}
    </LiteShell>
  );
}
