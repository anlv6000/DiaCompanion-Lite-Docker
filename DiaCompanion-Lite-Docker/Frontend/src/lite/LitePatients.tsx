import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useData } from "@/contexts/DataContext";
import { useToast } from "@/contexts/ToastContext";
import { useAsync, useDebounce } from "@/lib/hooks";
import {
  Button,
  DataTable,
  Field,
  GradeBadge,
  LoadState,
  PageHeader,
  Pagination,
  Panel,
} from "@/components/ui";
import { diabetesTypes, genders, label } from "@/lib/enums";
import { fmtDate } from "@/lib/format";
import type { CreatePatientRequest } from "@/types/api";

/* ------------------------------------------------------------------ danh sách */
export function LitePatientsPage() {
  const data = useData();
  const navigate = useNavigate();
  const [q, setQ] = useState("");
  const dq = useDebounce(q);
  const [page, setPage] = useState(1);

  useEffect(() => setPage(1), [dq]);

  const list = useAsync(
    () =>
      data.patients.list({
        q: dq.trim().length >= 2 ? dq.trim() : undefined,
        page,
        pageSize: 25,
      }),
    [dq, page],
  );

  return (
    <>
      <PageHeader
        title="Bệnh nhân & ca thu thập"
        subtitle="Chọn bệnh nhân để nhập chỉ số, nạp ảnh đáy mắt hai mắt và chạy AI."
        actions={
          <Button kind="primary" onClick={() => navigate("/cases/new")}>
            Thêm bệnh nhân
          </Button>
        }
      />
      <Panel>
        <div className="toolbar">
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Tìm theo mã, họ tên hoặc số điện thoại (từ 2 ký tự)"
            aria-label="Tìm bệnh nhân"
          />
        </div>
        <LoadState
          loading={list.loading}
          error={list.error}
          empty={!list.data?.items.length}
          onRetry={list.reload}
          emptyText="Chưa có bệnh nhân. Bấm “Thêm bệnh nhân” để tạo hồ sơ đầu tiên."
        >
          <DataTable
            headers={["Mã", "Họ tên", "Tuổi", "Giới", "Loại ĐTĐ", "Số năm mắc", "DR đã xác nhận", "Lần khám gần nhất", ""]}
          >
            {list.data?.items.map((p) => (
              <tr key={p.id}>
                <td className="mono">{p.code}</td>
                <td>{p.fullName}</td>
                <td className="mono">{p.age}</td>
                <td>{label(genders, p.gender)}</td>
                <td>{label(diabetesTypes, p.diabetesType)}</td>
                <td className="mono">{p.diabetesDurationYears ?? "—"}</td>
                <td>
                  <GradeBadge grade={p.latestDrGrade} />
                </td>
                <td className="mono">{fmtDate(p.latestVisitDate)}</td>
                <td>
                  <Button kind="primary" onClick={() => navigate(`/cases/${p.id}`)}>
                    Mở ca
                  </Button>
                </td>
              </tr>
            ))}
          </DataTable>
          {list.data && (
            <Pagination
              page={list.data.page}
              pageSize={list.data.pageSize}
              total={list.data.totalItems}
              totalPages={list.data.totalPages}
              rangeLabel={list.data.rangeLabel}
              onPage={setPage}
            />
          )}
        </LoadState>
      </Panel>
    </>
  );
}

/* ------------------------------------------------------------------ tạo / sửa */
type Form = Omit<CreatePatientRequest, "createAccount" | "existingUserId">;

const EMPTY: Form = {
  fullName: "",
  gender: 0,
  dateOfBirth: "",
  phone: "",
  address: "",
  diabetesType: 2,
  diabetesDurationYears: null,
  baselineHbA1c: null,
  note: "",
};

const num = (v: string): number | null => (v === "" ? null : Number(v));

export function LitePatientFormPage({ id }: { id?: number }) {
  const data = useData();
  const toast = useToast();
  const navigate = useNavigate();
  const edit = id != null;
  const [form, setForm] = useState<Form>(EMPTY);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const detail = useAsync(async () => (edit ? data.patients.get(id!) : null), [id]);
  const patch = <K extends keyof Form>(k: K, v: Form[K]) => setForm((f) => ({ ...f, [k]: v }));

  useEffect(() => {
    const d = detail.data;
    if (!d) return;
    setForm({
      fullName: d.fullName,
      gender: d.gender,
      dateOfBirth: (d.dateOfBirth || "").slice(0, 10),
      phone: d.phone,
      address: d.address ?? "",
      diabetesType: d.diabetesType,
      diabetesDurationYears: d.diabetesDurationYears ?? null,
      baselineHbA1c: d.baselineHbA1c ?? null,
      note: d.note ?? "",
    });
  }, [detail.data]);

  async function save(e: FormEvent) {
    e.preventDefault();
    setError("");
    if (!form.fullName.trim()) return setError("Vui lòng nhập họ tên.");
    if (!form.dateOfBirth) return setError("Vui lòng chọn ngày sinh.");
    if (!/^\d{10,11}$/.test(form.phone.trim())) return setError("Số điện thoại phải gồm 10 đến 11 chữ số.");
    setBusy(true);
    try {
      const body = {
        ...form,
        fullName: form.fullName.trim(),
        phone: form.phone.trim(),
        address: form.address?.trim() || null,
        note: form.note?.trim() || null,
      };
      if (edit) {
        const d = detail.data;
        if (!d) throw new Error("Chưa tải xong hồ sơ.");
        await data.patients.update(id!, { ...body, rowVersion: d.rowVersion });
        toast.push("Đã cập nhật hồ sơ.", "success");
        navigate(`/cases/${id}`);
      } else {
        // Bản thu thập dữ liệu KHÔNG cấp tài khoản đăng nhập cho bệnh nhân.
        const res = await data.patients.create({ ...body, createAccount: false, existingUserId: null });
        toast.push("Đã tạo hồ sơ bệnh nhân.", "success");
        navigate(`/cases/${res.patient.id}`);
      }
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  if (edit && (detail.loading || detail.error)) {
    return <LoadState loading={detail.loading} error={detail.error} onRetry={detail.reload}>{null}</LoadState>;
  }

  return (
    <>
      <PageHeader
        title={edit ? "Cập nhật hồ sơ bệnh nhân" : "Thêm bệnh nhân"}
        subtitle="Các trường lâm sàng (loại ĐTĐ, số năm mắc, HbA1c nền) được dùng để tính điểm nguy cơ của AI."
      />
      <Panel>
        <form onSubmit={save}>
          <div className="form-row three">
            <Field labelText="Họ tên" required>
              <input value={form.fullName} maxLength={70} onChange={(e) => patch("fullName", e.target.value)} />
            </Field>
            <Field labelText="Giới tính" required>
              <select value={form.gender} onChange={(e) => patch("gender", Number(e.target.value))}>
                {genders.map((g, i) => (
                  <option key={i} value={i}>{g}</option>
                ))}
              </select>
            </Field>
            <Field labelText="Ngày sinh" required>
              <input type="date" value={form.dateOfBirth} onChange={(e) => patch("dateOfBirth", e.target.value)} />
            </Field>
            <Field labelText="Số điện thoại" required help="10–11 chữ số.">
              <input inputMode="numeric" value={form.phone} maxLength={11} onChange={(e) => patch("phone", e.target.value.replace(/\D/g, ""))} />
            </Field>
            <Field labelText="Địa chỉ">
              <input value={form.address ?? ""} maxLength={300} onChange={(e) => patch("address", e.target.value)} />
            </Field>
            <Field labelText="Loại đái tháo đường" required help="Type 1 và Type 2 dùng luật nguy cơ khác nhau.">
              <select value={form.diabetesType} onChange={(e) => patch("diabetesType", Number(e.target.value))}>
                {diabetesTypes.map((x, i) => (
                  <option key={i} value={i}>{x}</option>
                ))}
              </select>
            </Field>
            <Field labelText="Thời gian mắc bệnh (năm)">
              <input type="number" min="0" max="100" value={form.diabetesDurationYears ?? ""} onChange={(e) => patch("diabetesDurationYears", num(e.target.value))} />
            </Field>
            <Field labelText="HbA1c nền (%)">
              <input type="number" step="0.1" min="3" max="20" value={form.baselineHbA1c ?? ""} onChange={(e) => patch("baselineHbA1c", num(e.target.value))} />
            </Field>
            <Field labelText="Ghi chú">
              <textarea value={form.note ?? ""} maxLength={1000} onChange={(e) => patch("note", e.target.value)} />
            </Field>
          </div>
          {error && <div className="state error" style={{ marginTop: 12 }}>{error}</div>}
          <div className="modal-actions" style={{ marginTop: 16 }}>
            <Button type="button" onClick={() => navigate(edit ? `/cases/${id}` : "/cases")}>Hủy</Button>
            <Button kind="primary" type="submit" busy={busy}>
              {edit ? "Lưu hồ sơ" : "Tạo hồ sơ"}
            </Button>
          </div>
        </form>
      </Panel>
    </>
  );
}
