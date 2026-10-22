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
import { isConflict } from "@/api/client";
import { birthYearToDob, placeholderPhone } from "@/lite/liteData";

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
            placeholder="Tìm theo mã BN hoặc mã nghiên cứu (từ 2 ký tự)"
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
            headers={["Mã BN", "Mã nghiên cứu", "Tuổi", "Giới", "Loại ĐTĐ", "Số năm mắc", "DR đã xác nhận", "Lần khám gần nhất", ""]}
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
/* Bản nghiên cứu: hồ sơ ẩn danh. Không nhập họ tên, SĐT, địa chỉ.
   - "Mã nghiên cứu" (do bệnh viện cấp, đối chiếu với danh tính ở bảng mã giữ NGOÀI hệ thống)
     được lưu vào trường họ tên.
   - Chỉ nhập năm sinh; ngày sinh quy ước 01/07 năm đó.
   - SĐT là số giả bắt đầu "00" (backend yêu cầu SĐT duy nhất). */
interface Form {
  researchCode: string;
  birthYear: string;
  gender: number;
  diabetesType: number;
  diabetesDurationYears: number | null;
  baselineHbA1c: number | null;
  note: string;
}

const EMPTY: Form = {
  researchCode: "",
  birthYear: "",
  gender: 0,
  diabetesType: 2,
  diabetesDurationYears: null,
  baselineHbA1c: null,
  note: "",
};

const num = (v: string): number | null => (v === "" ? null : Number(v));
const thisYear = new Date().getFullYear();

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
      researchCode: d.fullName,
      birthYear: (d.dateOfBirth || "").slice(0, 4),
      gender: d.gender,
      diabetesType: d.diabetesType,
      diabetesDurationYears: d.diabetesDurationYears ?? null,
      baselineHbA1c: d.baselineHbA1c ?? null,
      note: d.note ?? "",
    });
  }, [detail.data]);

  async function save(e: FormEvent) {
    e.preventDefault();
    setError("");
    const code = form.researchCode.trim();
    const year = Number(form.birthYear);
    if (!code) return setError("Nhập mã nghiên cứu.");
    if (!Number.isInteger(year) || year < thisYear - 110 || year > thisYear) return setError("Năm sinh không hợp lệ.");
    const common = {
      fullName: code,
      gender: form.gender,
      dateOfBirth: birthYearToDob(year),
      address: null,
      diabetesType: form.diabetesType,
      diabetesDurationYears: form.diabetesDurationYears,
      baselineHbA1c: form.baselineHbA1c,
      note: form.note.trim() || null,
    };
    setBusy(true);
    try {
      if (edit) {
        const d = detail.data;
        if (!d) throw new Error("Chưa tải xong hồ sơ.");
        await data.patients.update(id!, { ...common, phone: d.phone, rowVersion: d.rowVersion });
        toast.push("Đã cập nhật hồ sơ.", "success");
        navigate(`/cases/${id}`);
      } else {
        // SĐT giả có thể trùng (rất hiếm): thử lại vài lần với số khác.
        let lastErr: unknown;
        for (let i = 0; i < 3; i++) {
          try {
            const res = await data.patients.create({ ...common, phone: placeholderPhone(), createAccount: false, existingUserId: null });
            toast.push("Đã tạo hồ sơ bệnh nhân.", "success");
            navigate(`/cases/${res.patient.id}`);
            return;
          } catch (err) {
            lastErr = err;
            if (!isConflict(err)) break;
          }
        }
        throw lastErr;
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
        subtitle="Hồ sơ ẩn danh: không nhập họ tên, số điện thoại, địa chỉ. Bảng đối chiếu mã nghiên cứu với danh tính do bệnh viện giữ ngoài hệ thống."
      />
      <Panel>
        <form onSubmit={save}>
          <div className="form-row three">
            <Field labelText="Mã nghiên cứu" required help="Mã ghi trên phiếu đồng ý tham gia, ví dụ NC-0012.">
              <input value={form.researchCode} maxLength={70} autoFocus={!edit} onChange={(e) => patch("researchCode", e.target.value)} />
            </Field>
            <Field labelText="Năm sinh" required>
              <input type="number" inputMode="numeric" min={thisYear - 110} max={thisYear} value={form.birthYear} onChange={(e) => patch("birthYear", e.target.value)} />
            </Field>
            <Field labelText="Giới tính" required>
              <select value={form.gender} onChange={(e) => patch("gender", Number(e.target.value))}>
                {genders.map((g, i) => (
                  <option key={i} value={i}>{g}</option>
                ))}
              </select>
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
            <Field labelText="Ghi chú" help="Không ghi thông tin định danh.">
              <textarea value={form.note} maxLength={1000} onChange={(e) => patch("note", e.target.value)} />
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
