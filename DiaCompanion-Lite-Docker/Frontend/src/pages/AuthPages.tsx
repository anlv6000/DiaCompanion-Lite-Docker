import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { resolveLandingRoute } from "@/lib/permissions";
import { Field, Button, Panel, PageHeader, StatusBadge } from "@/components/ui";
import { authApi } from "@/api/services";


/* Đăng nhập — CHỈ nhân viên bằng email. Web bệnh viện không có luồng bệnh nhân
   (OTP/số điện thoại), luồng đó thuộc app bệnh nhân riêng. */
export function ForgotPasswordPage() {
  const navigate = useNavigate();
const [showNewPassword, setShowNewPassword] = useState(false);
const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [phone, setPhone] = useState("");
  const [code, setCode] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirm, setConfirm] = useState("");

  const [otpSent, setOtpSent] = useState(false);
  const [devCode, setDevCode] = useState<string | null>(null);

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  const requestOtp = async () => {
    if (!phone.trim()) {
      setError("Vui lòng nhập số điện thoại.");
      return;
    }

    setBusy(true);
    setError("");
    setMessage("");

    try {
      const res =
        await authApi.forgotPassword(
          phone.trim(),
        );

      setOtpSent(true);
      setMessage(res.message);

      if (res.devCode) {
        setDevCode(res.devCode);
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const resetPassword = async (
    e: FormEvent,
  ) => {
    e.preventDefault();

    if (!code.trim()) {
      setError("Vui lòng nhập mã OTP.");
      return;
    }

    if (!newPassword) {
      setError("Vui lòng nhập mật khẩu mới.");
      return;
    }

    if (newPassword !== confirm) {
      setError(
        "Mật khẩu xác nhận không khớp.",
      );
      return;
    }

    setBusy(true);
    setError("");

    try {
      await authApi.resetPassword({
        phone: phone.trim(),
        code: code.trim(),
        newPassword,
      });

      navigate("/login", {
        replace: true,
        state: {
          message:
            "Đặt lại mật khẩu thành công. Vui lòng đăng nhập.",
        },
      });
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="login">
      <div className="login-card">
        <div className="login-side">
          <h1>DiaCompanion</h1>
          <p>
            Khôi phục mật khẩu bằng số điện thoại
            đã đăng ký.
          </p>
        </div>

        <form
          className="login-form"
          onSubmit={resetPassword}
        >
          <h2 className="serif">
            Quên mật khẩu
          </h2>

          <p className="faint">
            Nhập số điện thoại của tài khoản để
            nhận mã xác minh.
          </p>

          <Field
            labelText="Số điện thoại"
            required
          >
            <input
              autoFocus
              type="tel"
              value={phone}
              disabled={otpSent}
              onChange={(e) =>
                setPhone(e.target.value)
              }
              placeholder="0912345678"
            />
          </Field>

          {!otpSent && (
            <Button
              type="button"
              kind="primary"
              busy={busy}
              onClick={requestOtp}
              disabled={!phone.trim()}
            >
              Gửi mã OTP
            </Button>
          )}

          {otpSent && (
            <>
              <Field
                labelText="Mã OTP"
                required
              >
                <input
                  value={code}
                  onChange={(e) =>
                    setCode(e.target.value)
                  }
                  placeholder="Nhập mã xác minh"
                />
              </Field>

              <Field labelText="Mật khẩu mới" required>
  <div className="input-with-action">
    <input
      type={showNewPassword ? "text" : "password"}
      value={newPassword}
      onChange={(e) => setNewPassword(e.target.value)}
      autoComplete="new-password"
    />

    <Button
      type="button"
      onClick={() => setShowNewPassword((x) => !x)}
    >
      {showNewPassword ? "Ẩn" : "👁"}
    </Button>
  </div>
</Field>

<Field labelText="Nhập lại mật khẩu mới" required>
  <div className="input-with-action">
    <input
      type={showConfirmPassword ? "text" : "password"}
      value={confirm}
      onChange={(e) => setConfirm(e.target.value)}
      autoComplete="new-password"
    />

    <Button
      type="button"
      onClick={() => setShowConfirmPassword((x) => !x)}
    >
      {showConfirmPassword ? "Ẩn" : "👁"}
    </Button>
  </div>
</Field>

              <Button
                type="submit"
                kind="primary"
                busy={busy}
                disabled={
                  !code.trim() ||
                  !newPassword ||
                  !confirm
                }
              >
                Đặt lại mật khẩu
              </Button>

              <Button
                type="button"
                onClick={requestOtp}
                disabled={busy}
              >
                Gửi lại mã OTP
              </Button>
            </>
          )}

          {message && (
            <div className="state success">
              {message}
            </div>
          )}

          {devCode && (
            <div className="state">
              Development OTP:{" "}
              <b>{devCode}</b>
            </div>
          )}

          {error && (
            <div className="state error">
              {error}
            </div>
          )}

          <div style={{ marginTop: 16 }}>
            <Link to="/login">
              ← Quay lại đăng nhập
            </Link>
          </div>
        </form>
      </div>
    </div>
  );
}
   export function LoginPage() {
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
          <p>Console lâm sàng hỗ trợ sàng lọc bệnh võng mạc đái tháo đường.</p>
          <div className="stack">
            <StatusBadge text="AI chỉ hỗ trợ quyết định" kind="defer" />
            <span>• Tin cậy và bất đồng hiển thị trực tiếp.</span>  
          </div>
        </div>
        <form className="login-form" onSubmit={submit}>
          <h2 className="serif">Đăng nhập</h2>
          <p className="faint">
            Dành cho nhân viên bệnh viện (Bác sĩ, Lễ tân, Quản trị).
          </p>
          <Field labelText="Email hoặc số điện thoại" required>
  <input
    autoFocus
    type="text"
    value={loginId}
    onChange={(e) => setLoginId(e.target.value)}
    placeholder="Email hoặc số điện thoại"
    autoComplete="username"
  />
</Field>
          <Field labelText="Mật khẩu" required>
            <div className="input-with-action">
              <input
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
              <Button type="button" onClick={() => setShowPassword((x) => !x)}>
                {showPassword ? "Ẩn" : "Hiện"}
              </Button>
            </div>
          </Field>
          {error && <div className="state error">{error}</div>}
          <div
  style={{
    display: "flex",
    justifyContent: "flex-end",
    marginTop: 4,
    marginBottom: 12,
  }}
>
  <Link to="/forgot-password">
    Quên mật khẩu?
  </Link>
</div>
          <Button
            kind="primary"
            type="submit"
            busy={busy}
            disabled={!loginId.trim() || !password}
            style={{ width: "100%", justifyContent: "center" }}
          >
            Đăng nhập
          </Button>
          <div className="split" style={{ marginTop: 10 }}>

          </div>
        </form>
      </div>
    </div>
  );
}

/* Đổi mật khẩu khi đã đăng nhập (bắt buộc nếu đang dùng mật khẩu tạm). */
export function ChangePasswordPage() {
  const [showNewPassword, setShowNewPassword] = useState(false);
const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const { user, changePassword } = useAuth();
  const navigate = useNavigate();
  const toast = useToast();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (next !== confirm) {
      setError("Hai mật khẩu mới chưa trùng khớp.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      await changePassword({
        ...(user?.mustChangePassword ? {} : { currentPassword: current }),
        newPassword: next,
      });
      toast.push("Đã cập nhật mật khẩu.", "success");
      setCurrent("");
      setNext("");
      setConfirm("");
      // Điều hướng theo vai trò về trang chủ an toàn (không hardcode /triage,
      // không rơi vào /home vốn không tồn tại trên web console).
      navigate(resolveLandingRoute(user, user?.defaultRoute), {
        replace: true,
      });
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <PageHeader
        title="Đổi mật khẩu"
        subtitle={
          user?.mustChangePassword
            ? "Bạn phải đổi mật khẩu tạm trước khi tiếp tục."
            : "Cập nhật thông tin xác thực của tài khoản đang đăng nhập."
        }
      />
      <Panel>
        <form onSubmit={submit} style={{ maxWidth: 460 }}>
          {!user?.mustChangePassword && (
            <Field labelText="Mật khẩu hiện tại" required>
              <input
                type="password"
                value={current}
                onChange={(e) => setCurrent(e.target.value)}
              />
            </Field>
          )}
          <Field labelText="Mật khẩu mới" required>
  <div className="input-with-action">
    <input
      type={showNewPassword ? "text" : "password"}
      value={next}
      onChange={(e) =>
        setNext(e.target.value)
      }
      autoComplete="new-password"
    />

    <Button
      type="button"
      onClick={() =>
        setShowNewPassword((x) => !x)
      }
    >
      {showNewPassword ? "Ẩn" : "Hiện"}
    </Button>
  </div>
</Field>
          <Field labelText="Nhập lại mật khẩu mới" required>
  <div className="input-with-action">
    <input
      type={showConfirmPassword ? "text" : "password"}
      value={confirm}
      onChange={(e) =>
        setConfirm(e.target.value)
      }
      autoComplete="new-password"
    />

    <Button
      type="button"
      onClick={() =>
        setShowConfirmPassword((x) => !x)
      }
    >
      {showConfirmPassword ? "Ẩn" : "Hiện"}
    </Button>
  </div>
</Field>
          {error && <div className="state error">{error}</div>}
          <Button kind="primary" type="submit" busy={busy}>
            Cập nhật mật khẩu
          </Button>
        </form>
      </Panel>
    </>
  );
}
