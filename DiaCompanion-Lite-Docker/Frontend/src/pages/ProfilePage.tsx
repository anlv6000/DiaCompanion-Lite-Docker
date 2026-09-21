import { useEffect, useState, type FormEvent } from "react";
import { authApi } from "@/api/services";
import { PageHeader, Panel, Field, Button, LoadState } from "@/components/ui";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { useAsync } from "@/lib/hooks";
import { fmtDate, initials } from "@/lib/format";
import { roleLabel } from "@/lib/roles";
import type { StaffProfileDto } from "@/types/api";

export function ProfilePage() {
  const toast = useToast();
  const { refresh } = useAuth();
  const profile = useAsync<StaffProfileDto>(() => authApi.profile(), []);

  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [licenseNo, setLicenseNo] = useState("");
  const [busy, setBusy] = useState(false);
  const [formError, setFormError] = useState("");

  useEffect(() => {
    if (!profile.data) return;
    setFullName(profile.data.fullName || "");
    setPhone(profile.data.phone || "");
    setLicenseNo(profile.data.licenseNo || "");
  }, [profile.data]);

  const isDoctor = profile.data?.role === "Doctor";

  const save = async (e: FormEvent) => {
    e.preventDefault();
    if (!profile.data) return;

    const name = fullName.trim();
    const normalizedPhone = phone.trim();
    const license = licenseNo.trim();

    if (!name) {
      setFormError("Vui lòng nhập họ tên.");
      return;
    }
    if (!/^\d{10,11}$/.test(normalizedPhone)) {
      setFormError("Số điện thoại phải gồm 10 đến 11 chữ số.");
      return;
    }
    if (isDoctor && !license) {
      setFormError("Bác sĩ phải có số chứng chỉ hành nghề.");
      return;
    }

    setBusy(true);
    setFormError("");
    try {
      const updated = await authApi.updateProfile({
        fullName: name,
        phone: normalizedPhone,
        licenseNo: isDoctor ? license : null,
        rowVersion: profile.data.rowVersion,
      });
      profile.setData(updated);
      await refresh();
      toast.push("Đã cập nhật hồ sơ cá nhân.", "success");
    } catch (err) {
      const message = (err as Error).message;
      setFormError(message);
      toast.push(message, "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader
        title="Hồ sơ cá nhân"
        subtitle="Thông tin tài khoản của Bác sĩ/Lễ tân đang đăng nhập. Email và vai trò do Admin quản lý."
      />

      <LoadState
        loading={profile.loading}
        error={profile.error}
        empty={!profile.data}
        onRetry={profile.reload}
      >
        {profile.data && (
          <div className="rail">
            <div>
              <Panel title="Thông tin cá nhân">
                <form onSubmit={save}>
                  <div className="grid2">
                    <Field labelText="Họ và tên" required>
                      <input
                        value={fullName}
                        onChange={(e) => setFullName(e.target.value)}
                        maxLength={200}
                      />
                    </Field>

                    <Field
                      labelText="Số điện thoại"
                      required
                      help="Chỉ nhập 10 đến 11 chữ số."
                    >
                      <input
                        value={phone}
                        onChange={(e) => setPhone(e.target.value.replace(/\D/g, "").slice(0, 11))}
                        inputMode="numeric"
                        maxLength={11}
                      />
                    </Field>

                    <Field
                      labelText="Email đăng nhập"
                      help="Không thể tự thay đổi email đăng nhập."
                    >
                      <input value={profile.data.email || ""} disabled />
                    </Field>

                    <Field labelText="Vai trò">
                      <input value={roleLabel(profile.data.role)} disabled />
                    </Field>

                    {isDoctor && (
                      <Field labelText="Số chứng chỉ hành nghề" required>
                        <input
                          value={licenseNo}
                          onChange={(e) => setLicenseNo(e.target.value)}
                          maxLength={50}
                        />
                      </Field>
                    )}
                  </div>

                  {formError && <div className="state error" style={{ marginBottom: 10 }}>{formError}</div>}

                  <div className="page-actions">
                    <Button type="submit" kind="primary" busy={busy}>
                      Lưu thay đổi
                    </Button>
                  </div>
                </form>
              </Panel>
            </div>

            <div>
              <Panel title="Tài khoản">
                <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 14 }}>
                  <span className="avatar" style={{ width: 48, height: 48, fontSize: 16 }}>
                    {initials(profile.data.fullName)}
                  </span>
                  <div>
                    <strong>{profile.data.fullName}</strong>
                    <small style={{ display: "block" }}>{roleLabel(profile.data.role)}</small>
                  </div>
                </div>

                <div className="stack">
                  <AccountInfo label="Mã tài khoản" value={`#${profile.data.id}`} />
                  <AccountInfo label="Email" value={profile.data.email || "—"} />
                  <AccountInfo label="Đăng nhập gần nhất" value={fmtDate(profile.data.lastLoginAt, true)} />
                  <AccountInfo label="Ngày tạo tài khoản" value={fmtDate(profile.data.createdAt, true)} />
                </div>
              </Panel>
            </div>
          </div>
        )}
      </LoadState>
    </>
  );
}

function AccountInfo({ label, value }: { label: string; value: string }) {
  return (
    <div className="detail-item">
      <small>{label}</small>
      <strong>{value || "—"}</strong>
    </div>
  );
}
