import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";
import { useData } from "@/contexts/DataContext";
import { useToast } from "@/contexts/ToastContext";
import { useAsync } from "@/lib/hooks";
import {
  Button,
  DataTable,
  EyeBadge,
  Field,
  GradeBadge,
  LoadState,
  PageHeader,
  Panel,
  StatusBadge,
} from "@/components/ui";
import { ProtectedImage } from "@/components/ProtectedImage";
import { LineChart } from "@/components/charts";
import { diabetesTypes, eyes, genders, label, metricContexts, qualityStatuses, visitStatuses } from "@/lib/enums";
import { fmtDate, num } from "@/lib/format";
import type { AiDiagnosisDto, FundusImageDto, PatientDetailDto, VisitDto } from "@/types/api";
import { metricTypes } from "@/lib/enums";

/* Khớp enum ReferralType của backend (None=0, FollowUp=1, Ophthalmology=2, Urgent=3).
   Không dùng `referralTypes` trong lib/enums.ts vì mảng đó chỉ có 3 phần tử nên
   lệch nhãn từ giá trị 1 trở đi. */
const referralTypes = ["Không", "Theo dõi", "Chuyên khoa mắt", "Khẩn cấp"];

/* Trang làm việc của MỘT bệnh nhân trong bản thu thập dữ liệu:
   1) bắt đầu lượt  →  2) nhập chỉ số  →  3) nạp ảnh mắt phải + mắt trái
   →  4) chạy AI  →  5) bác sĩ xác nhận (trang /fundus/:id)  →  6) đóng lượt.
   Toàn bộ nghiệp vụ và quy tắc nằm ở backend hiện có; trang này chỉ gọi lại
   đúng các API đó. */
export function LiteCasePage({ id }: { id: number }) {
  const data = useData();
  const { user } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();
  const patient = useAsync(() => data.patients.get(id), [id]);
  const visits = useAsync(() => data.visits.list({ patientId: id, page: 1, pageSize: 100 }), [id]);
  const [starting, setStarting] = useState(false);
  const [tab, setTab] = useState<"visit" | "results" | "progression" | "metrics">("visit");

  const reloadAll = () => {
    visits.reload();
    patient.reload();
  };

  const items = visits.data?.items ?? [];
  const open = items.find((v) => v.status === 0);
  const closed = items.filter((v) => v.status !== 0);

  const start = async () => {
    setStarting(true);
    try {
      await data.visits.create({ patientId: id, doctorId: user?.userId });
      toast.push("Đã bắt đầu lượt thu thập mới.", "success");
      visits.reload();
      patient.reload();
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setStarting(false);
    }
  };

  return (
    <LoadState loading={patient.loading} error={patient.error} onRetry={patient.reload} empty={!patient.data}>
      {patient.data && (
        <>
          <PageHeader
            title={`${patient.data.code} · ${patient.data.fullName}`}
            subtitle={summary(patient.data)}
            actions={
              <>
                <Button onClick={() => navigate("/cases")}>Danh sách</Button>
                <Button onClick={() => navigate(`/cases/${id}/edit`)}>Sửa hồ sơ</Button>
              </>
            }
          />

          <div className="tabs" role="tablist">
            {([
              ["visit", "Lượt hiện tại"],
              ["results", "Kết quả đã xác nhận"],
              ["progression", "Diễn tiến"],
              ["metrics", "Chỉ số theo thời gian"],
            ] as const).map(([key, txt]) => (
              <button
                key={key}
                role="tab"
                aria-selected={tab === key}
                className={`tab ${tab === key ? "on" : ""}`}
                onClick={() => setTab(key)}
              >
                {txt}
              </button>
            ))}
          </div>

          {tab === "visit" && (
            <LoadState loading={visits.loading} error={visits.error} onRetry={visits.reload}>
              {open ? (
                <VisitWorkspace patient={patient.data} visit={open} onChanged={reloadAll} />
              ) : (
                <Panel title="Lượt thu thập">
                  <p className="muted">
                    Bệnh nhân chưa có lượt đang mở. Mỗi lượt gồm chỉ số lâm sàng, ảnh mắt phải/mắt trái và kết quả AI.
                  </p>
                  <Button kind="primary" busy={starting} onClick={start}>
                    Bắt đầu lượt thu thập mới
                  </Button>
                </Panel>
              )}

              {closed.length > 0 && (
                <Panel title="Các lượt đã đóng">
                  <DataTable headers={["Mã", "Ngày khám", "Ảnh", "Kết luận", "Chuyển tuyến", "Trạng thái"]}>
                    {closed.map((v) => (
                      <tr key={v.id}>
                        <td className="mono">#{v.id}</td>
                        <td className="mono">{fmtDate(v.visitDate, true)}</td>
                        <td className="mono">{v.imageCount}</td>
                        <td className="wrap-text">{v.conclusion || "—"}</td>
                        <td>{label(referralTypes, v.referral)}</td>
                        <td>
                          <StatusBadge text={label(visitStatuses, v.status)} kind="ok" />
                        </td>
                      </tr>
                    ))}
                  </DataTable>
                </Panel>
              )}
            </LoadState>
          )}

          {tab === "results" && <ConfirmedResultsPanel patientId={id} visits={items} />}
          {tab === "progression" && <ProgressionPanel patientId={id} />}
          {tab === "metrics" && <MetricHistoryPanel patientId={id} visits={items} />}
        </>
      )}
    </LoadState>
  );
}

function summary(p: PatientDetailDto) {
  const parts = [
    label(genders, p.gender),
    `${p.age} tuổi`,
    label(diabetesTypes, p.diabetesType),
    p.diabetesDurationYears != null ? `mắc ${p.diabetesDurationYears} năm` : null,
    p.baselineHbA1c != null ? `HbA1c nền ${p.baselineHbA1c}%` : null,
  ];
  return parts.filter(Boolean).join(" · ");
}

/* ---------------------------------------------------------------- lượt đang mở */
function VisitWorkspace({
  patient,
  visit,
  onChanged,
}: {
  patient: PatientDetailDto;
  visit: VisitDto;
  onChanged: () => void;
}) {
  return (
    <>
      <MetricsPanel visit={visit} />
      <EyesPanel patient={patient} visit={visit} onChanged={onChanged} />
      <ClosePanel visit={visit} onClosed={onChanged} />
    </>
  );
}

/* ---------------------------------------------------------------- 1. chỉ số */
function MetricsPanel({ visit }: { visit: VisitDto }) {
  const data = useData();
  const toast = useToast();
  const metrics = useAsync(() => data.visits.healthMetrics(visit.id), [visit.id]);
  const [hba1c, setHba1c] = useState("");
  const [glucose, setGlucose] = useState("");
  const [glucoseContext, setGlucoseContext] = useState("");
  const [systolic, setSystolic] = useState("");
  const [diastolic, setDiastolic] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    const m = metrics.data;
    if (!m) return;
    setHba1c(m.hbA1c?.value == null ? "" : String(m.hbA1c.value));
    setGlucose(m.glucose?.value == null ? "" : String(m.glucose.value));
    setGlucoseContext(m.glucose?.context == null ? "" : String(m.glucose.context));
    setSystolic(m.bloodPressure?.systolicValue == null ? "" : String(m.bloodPressure.systolicValue));
    setDiastolic(m.bloodPressure?.diastolicValue == null ? "" : String(m.bloodPressure.diastolicValue));
  }, [metrics.data]);

  const nullable = (v: string): number | null => {
    if (!v.trim()) return null;
    const n = Number(v);
    return Number.isFinite(n) ? n : Number.NaN;
  };

  const save = async () => {
    const h = nullable(hba1c), g = nullable(glucose), s = nullable(systolic), d = nullable(diastolic);
    if ([h, g, s, d].some((x) => x != null && Number.isNaN(x))) return toast.push("Giá trị chỉ số phải là số hợp lệ.", "error");
    if (g != null && !glucoseContext) return toast.push("Đường huyết phải chọn thời điểm đo.", "error");
    if ((s == null) !== (d == null)) return toast.push("Huyết áp phải nhập đủ cả tâm thu và tâm trương.", "error");
    if (s != null && d != null && s <= d) return toast.push("Huyết áp tâm thu phải lớn hơn tâm trương.", "error");
    setSaving(true);
    try {
      const cur = metrics.data;
      await data.visits.saveHealthMetrics(visit.id, {
        glucose: g,
        glucoseContext: g == null ? null : Number(glucoseContext),
        glucoseNote: null,
        glucoseRowVersion: cur?.glucose?.rowVersion ?? null,
        hbA1c: h,
        hbA1cNote: null,
        hbA1cRowVersion: cur?.hbA1c?.rowVersion ?? null,
        systolicBp: s,
        diastolicBp: d,
        bloodPressureNote: null,
        systolicRowVersion: cur?.bloodPressure?.metricType === 3 ? cur.bloodPressure.rowVersion : cur?.bloodPressure?.pairRowVersion ?? null,
        diastolicRowVersion: cur?.bloodPressure?.metricType === 4 ? cur.bloodPressure.rowVersion : cur?.bloodPressure?.pairRowVersion ?? null,
      });
      toast.push("Đã lưu chỉ số của lượt.", "success");
      metrics.reload();
    } catch (e) {
      toast.push((e as Error).message, "error");
      metrics.reload(); // rowVersion có thể đã cũ
    } finally {
      setSaving(false);
    }
  };

  return (
    <Panel
      title={`1. Chỉ số lâm sàng — lượt #${visit.id}`}
      action={
        <Button kind="primary" busy={saving} onClick={save}>
          Lưu chỉ số
        </Button>
      }
    >
      <p className="muted">
        HbA1c và huyết áp được AI dùng để tính điểm nguy cơ (huyết áp chỉ với Type 2, cần ≥ 3 lần đo hợp lệ). Để trống nếu không đo.
      </p>
      <LoadState loading={metrics.loading} error={metrics.error} onRetry={metrics.reload}>
        <div className="form-row three">
          <Field labelText="HbA1c (%)">
            <input type="number" step="0.1" value={hba1c} onChange={(e) => setHba1c(e.target.value)} />
          </Field>
          <Field labelText="Huyết áp tâm thu (mmHg)">
            <input type="number" value={systolic} onChange={(e) => setSystolic(e.target.value)} />
          </Field>
          <Field labelText="Huyết áp tâm trương (mmHg)">
            <input type="number" value={diastolic} onChange={(e) => setDiastolic(e.target.value)} />
          </Field>
          <Field labelText="Đường huyết (mmol/L)">
            <input type="number" step="0.1" value={glucose} onChange={(e) => setGlucose(e.target.value)} />
          </Field>
          <Field labelText="Thời điểm đo đường huyết">
            <select value={glucoseContext} onChange={(e) => setGlucoseContext(e.target.value)}>
              {metricContexts.map((c, i) => (
                <option key={i} value={i === 0 ? "" : String(i)}>{c || "— chọn —"}</option>
              ))}
            </select>
          </Field>
        </div>
      </LoadState>
    </Panel>
  );
}

/* ---------------------------------------------------------------- 2. ảnh + AI */
interface Entry {
  img: FundusImageDto;
  dx: AiDiagnosisDto | null;
}

function EyesPanel({
  patient,
  visit,
  onChanged,
}: {
  patient: PatientDetailDto;
  visit: VisitDto;
  onChanged: () => void;
}) {
  const data = useData();
  const list = useAsync(async (): Promise<Entry[]> => {
    const imgs = await data.images.list({ patientId: patient.id, visitId: visit.id });
    return Promise.all(
      imgs.map(async (img) => {
        try {
          const all = await data.diagnoses.byImage(img.id);
          return { img, dx: all[0] ?? null };
        } catch {
          return { img, dx: null };
        }
      }),
    );
  }, [patient.id, visit.id]);

  const latest = (eye: number): Entry | null =>
    (list.data ?? []).filter((x) => x.img.eye === eye).sort((a, b) => b.img.id - a.img.id)[0] ?? null;

  const refresh = () => {
    list.reload();
    onChanged();
  };

  return (
    <Panel title="2. Ảnh đáy mắt — mắt phải (OD) và mắt trái (OS)">
      <p className="muted">
        Với mỗi mắt: nạp ảnh → duyệt chất lượng (Đạt) → chạy AI → mở chi tiết để bác sĩ xác nhận hoặc ghi đè phân độ.
      </p>
      <LoadState loading={list.loading} error={list.error} onRetry={list.reload}>
        <div className="grid2">
          {[0, 1].map((eye) => (
            <EyeCard key={eye} eye={eye} patientId={patient.id} visitId={visit.id} entry={latest(eye)} onChanged={refresh} />
          ))}
        </div>
      </LoadState>
    </Panel>
  );
}

function EyeCard({
  eye,
  patientId,
  visitId,
  entry,
  onChanged,
}: {
  eye: number;
  patientId: number;
  visitId: number;
  entry: Entry | null;
  onChanged: () => void;
}) {
  const data = useData();
  const toast = useToast();
  const navigate = useNavigate();
  const fileRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState("");

  const act = async (key: string, fn: () => Promise<void>) => {
    setBusy(key);
    try {
      await fn();
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusy("");
    }
  };

  const upload = () =>
    act("upload", async () => {
      if (!file) throw new Error("Chọn tệp ảnh JPG/PNG (tối đa 10 MB).");
      await data.images.upload(file, patientId, visitId, eye);
      toast.push(`Đã nạp ảnh ${eyes[eye]}.`, "success");
      setFile(null);
      if (fileRef.current) fileRef.current.value = "";
      onChanged();
    });

  const setQuality = (status: number) =>
    act("quality", async () => {
      if (!entry) return;
      await data.images.quality(entry.img.id, status, undefined, entry.img.rowVersion);
      toast.push(status === 1 ? "Ảnh đạt chất lượng." : "Đã đánh dấu ảnh không đạt.", "success");
      onChanged();
    });

  const run = () =>
    act("run", async () => {
      if (!entry) return;
      await data.diagnoses.run(entry.img.id);
      toast.push(`Đã chạy đủ 3 model AI cho ${eyes[eye]}.`, "success");
      onChanged();
    });

  const replace = () =>
    act("void", async () => {
      if (!entry) return;
      if (!window.confirm(`Thu hồi ảnh ${eyes[eye]} và kết quả AI liên quan để nạp ảnh khác?`)) return;
      await data.images.void(entry.img.id, "Thay ảnh (nạp nhầm hoặc chất lượng kém)", entry.img.rowVersion);
      toast.push("Đã thu hồi ảnh.", "success");
      onChanged();
    });

  const img = entry?.img;
  const dx = entry?.dx ?? null;
  const q = img?.qualityStatus;

  return (
    <div className="panel" style={{ margin: 0 }}>
      <div className="panel-h">
        <span>
          <EyeBadge eye={eye} /> {eye === 0 ? "Mắt phải" : "Mắt trái"}
        </span>
        {img && <StatusBadge text={label(qualityStatuses, q)} kind={q === 1 ? "ok" : q === 2 ? "alert" : "watch"} />}
      </div>
      <div className="panel-b">
        {!img ? (
          <>
            <Field labelText="Tệp ảnh" help="JPG/PNG, tối đa 10 MB.">
              <input ref={fileRef} type="file" accept="image/jpeg,image/png" onChange={(e) => setFile(e.target.files?.[0] || null)} />
            </Field>
            <Button kind="primary" busy={busy === "upload"} disabled={!file} onClick={upload}>
              Nạp ảnh {eyes[eye]}
            </Button>
          </>
        ) : (
          <>
            <ProtectedImage imageId={img.id} alt={`Ảnh đáy mắt ${eyes[eye]} #${img.id}`} onClick={() => navigate(dx ? `/fundus/${img.id}?diagnosis=${dx.id}` : `/fundus/${img.id}`)} />
            <div className="mono" style={{ margin: "4px 0 10px" }}>Ảnh #{img.id}</div>

            {q === 0 && (
              <div className="split" style={{ gap: 8, display: "flex", flexWrap: "wrap" }}>
                <Button kind="primary" busy={busy === "quality"} onClick={() => setQuality(1)}>Đạt chất lượng</Button>
                <Button busy={busy === "quality"} onClick={() => setQuality(2)}>Không đạt</Button>
              </div>
            )}
            {q === 1 && !dx && (
              <Button kind="primary" busy={busy === "run"} busyText="Đang chạy AI…" onClick={run}>
                Chạy AI
              </Button>
            )}
            {q === 2 && <p className="muted">Ảnh không đạt — thu hồi và nạp ảnh khác để phân tích.</p>}

            {dx && <DxSummary dx={dx} onOpen={() => navigate(`/fundus/${img.id}?diagnosis=${dx.id}`)} />}

            <div style={{ marginTop: 10, display: "flex", gap: 8, flexWrap: "wrap" }}>
              {!dx && <Button onClick={() => navigate(`/fundus/${img.id}`)}>Xem ảnh</Button>}
              <Button busy={busy === "void"} onClick={replace}>Thay ảnh</Button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function DxSummary({ dx, onOpen }: { dx: AiDiagnosisDto; onOpen: () => void }) {
  return (
    <div style={{ marginTop: 8 }}>
      <table>
        <tbody>
          <tr><td>Phân độ AI</td><td><GradeBadge grade={dx.drGrade} /></td></tr>
          <tr><td>Phân độ suy từ tổn thương</td><td><GradeBadge grade={dx.lesionGradeImplied} /></td></tr>
          <tr><td>Tổn thương MA / HE / EX / SE</td><td className="mono">{dx.countMA ?? "—"} / {dx.countHE ?? "—"} / {dx.countEX ?? "—"} / {dx.countSE ?? "—"}</td></tr>
          <tr><td>Bất đồng D ≥ ngưỡng áp dụng</td><td className="mono">{num(dx.disagreement)} ≥ {num(dx.effectiveDisagreementThreshold)}</td></tr>
          <tr><td>Điểm nguy cơ nền</td><td className="mono">{dx.clinicalRiskScore ?? "—"}</td></tr>
          <tr><td>Fractal dimension</td><td className="mono">{num(dx.fractalDimension, 3)}</td></tr>
          <tr>
            <td>Ưu tiên xem</td>
            <td>
              {dx.isDeferred ? <StatusBadge text={dx.deferReasonLabel || "Cần bác sĩ xem trước"} kind="defer" /> : <StatusBadge text="Thường quy" kind="ok" />}
            </td>
          </tr>
          <tr>
            <td>Xác nhận của bác sĩ</td>
            <td>
              {dx.isConfirmed && dx.review ? (
                <StatusBadge text={`${dx.review.actionLabel}: ${dx.review.finalGradeLabel}`} kind="ok" />
              ) : (
                <StatusBadge text="Chưa xác nhận" kind="watch" />
              )}
            </td>
          </tr>
        </tbody>
      </table>
      <div style={{ marginTop: 8 }}>
        <Button kind={dx.isConfirmed ? "default" : "primary"} onClick={onOpen}>
          {dx.isConfirmed ? "Xem chi tiết" : "Chi tiết & xác nhận"}
        </Button>
      </div>
    </div>
  );
}

/* ---------------------------------------------------------------- 3. đóng lượt */
function ClosePanel({ visit, onClosed }: { visit: VisitDto; onClosed: () => void }) {
  const data = useData();
  const toast = useToast();
  const [conclusion, setConclusion] = useState("");
  const [referral, setReferral] = useState(0);
  const [months, setMonths] = useState<number | "">("");
  const [busy, setBusy] = useState(false);

  const close = async () => {
    if (!conclusion.trim()) return toast.push("Kết luận là bắt buộc khi đóng lượt.", "error");
    setBusy(true);
    try {
      await data.visits.close(visit.id, {
        conclusion: conclusion.trim(),
        referral,
        recheckMonths: months === "" ? null : Number(months),
        rowVersion: visit.rowVersion,
      });
      toast.push("Đã đóng lượt thu thập.", "success");
      onClosed();
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Panel title="3. Đóng lượt">
      <p className="muted">
        Chỉ đóng được khi mọi ảnh đã duyệt chất lượng, đã chạy AI và mọi kết quả AI đã được bác sĩ xác nhận
        (hiện: {visit.imageCount} ảnh, {visit.pendingReviewCount} kết quả chờ xác nhận). Đóng lượt xong mới tạo được lượt mới cho cùng bệnh nhân.
      </p>
      <Field labelText="Kết luận" required>
        <textarea rows={3} value={conclusion} onChange={(e) => setConclusion(e.target.value)} />
      </Field>
      <div className="form-row two">
        <Field labelText="Chuyển tuyến">
          <select value={referral} onChange={(e) => setReferral(Number(e.target.value))}>
            {referralTypes.map((r, i) => (
              <option key={i} value={i}>{r}</option>
            ))}
          </select>
        </Field>
        <Field labelText="Tái khám sau (tháng)" help="Để trống để hệ thống tự xác định theo mức DR đã xác nhận.">
          <input type="number" min={1} max={24} value={months} onChange={(e) => setMonths(e.target.value ? Number(e.target.value) : "")} />
        </Field>
      </div>
      <Button kind="primary" busy={busy} onClick={close}>
        Đóng lượt thu thập
      </Button>
    </Panel>
  );
}

/* ------------------------------------------------- Kết quả đã xác nhận (kết quả) */
function ConfirmedResultsPanel({
  patientId,
  visits,
}: {
  patientId: number;
  visits: VisitDto[];
}) {
  const data = useData();
  const navigate = useNavigate();

  const list = useAsync(async () => {
    const imgs = await data.images.list({ patientId });
    const rows = await Promise.all(
      imgs.map(async (img) => {
        try {
          const dxs = await data.diagnoses.byImage(img.id);
          return { img, dx: dxs[0] ?? null };
        } catch {
          return { img, dx: null };
        }
      }),
    );
    // Chỉ giữ ảnh đã có kết quả AI đã được bác sĩ xác nhận.
    return rows.filter((r) => r.dx?.isConfirmed && r.dx.review);
  }, [patientId]);

  const visitDate = (visitId?: number | null) =>
    visits.find((v) => v.id === visitId)?.visitDate ?? null;

  return (
    <Panel title="Kết quả đã xác nhận">
      <p className="muted">
        Danh sách kết quả AI đã được bác sĩ phê duyệt hoặc ghi đè. Mở chi tiết để xem ảnh, tổn thương, mạch máu và phân độ cuối.
      </p>
      <LoadState
        loading={list.loading}
        error={list.error}
        empty={!list.data?.length}
        onRetry={list.reload}
        emptyText="Chưa có kết quả nào được xác nhận."
      >
        <DataTable
          headers={["Ngày khám", "Mắt", "Phân độ AI", "Phân độ cuối", "Hành động", "Ưu tiên", "Ảnh #", ""]}
        >
          {list.data?.map(({ img, dx }) => (
            <tr key={img.id}>
              <td className="mono">{fmtDate(visitDate(img.visitId))}</td>
              <td>
                <EyeBadge eye={img.eye} />
              </td>
              <td>
                <GradeBadge grade={dx!.drGrade} />
              </td>
              <td>
                <GradeBadge grade={dx!.review!.finalGrade} />
              </td>
              <td>{dx!.review!.actionLabel}</td>
              <td>
                {dx!.isDeferred ? (
                  <StatusBadge text={dx!.deferReasonLabel || "Đã chuyển bác sĩ"} kind="defer" />
                ) : (
                  <StatusBadge text="Thường quy" kind="ok" />
                )}
              </td>
              <td className="mono">{img.id}</td>
              <td>
                <Button onClick={() => navigate(`/fundus/${img.id}?diagnosis=${dx!.id}`)}>
                  Xem chi tiết
                </Button>
              </td>
            </tr>
          ))}
        </DataTable>
      </LoadState>
    </Panel>
  );
}

/* ------------------------------------------------------------------ Diễn tiến */
function ProgressionPanel({ patientId }: { patientId: number }) {
  const data = useData();
  const [months, setMonths] = useState(120);
  const prog = useAsync(() => data.diagnoses.progression(patientId, months), [patientId, months]);

  return (
    <>
      <Panel title="Diễn tiến DR">
        <div className="toolbar">
          <Field labelText="Khoảng thời gian" className="inline">
            <select value={months} onChange={(e) => setMonths(Number(e.target.value))}>
              <option value="3">3 tháng</option>
              <option value="6">6 tháng</option>
              <option value="12">12 tháng</option>
              <option value="24">24 tháng</option>
              <option value="120">Tất cả</option>
            </select>
          </Field>
        </div>
        <LoadState
          loading={prog.loading}
          error={prog.error}
          empty={!prog.data?.points.length}
          onRetry={prog.reload}
          emptyText="Chưa có kết quả đã xác nhận trong khoảng thời gian này."
        >
          {prog.data && (
            <>
              <LineChart
                series={[
                  { name: "DR grade", points: prog.data.points.map((p) => ({ x: p.date, y: p.confirmedGrade })) },
                  { name: "Fractal", kind: "defer", points: prog.data.points.map((p) => ({ x: p.date, y: p.fractalDimension })) },
                  { name: "HbA1c", kind: "alert", points: prog.data.points.map((p) => ({ x: p.date, y: p.hbA1c })) },
                ]}
              />
              {prog.data.trendWarning && (
                <div className="state error" style={{ marginTop: 10 }}>
                  {prog.data.trendWarning}
                </div>
              )}
            </>
          )}
        </LoadState>
      </Panel>
      <Panel title="Bảng số liệu đã xác nhận">
        <LoadState loading={prog.loading} error={prog.error} empty={!prog.data?.points.length}>
          <DataTable headers={["Ngày", "Lượt khám", "DR xác nhận", "Fractal", "HbA1c"]}>
            {prog.data?.points.map((p, i) => (
              <tr key={i}>
                <td className="mono">{fmtDate(p.date)}</td>
                <td className="mono">{p.visitId ? `#${p.visitId}` : "—"}</td>
                <td>
                  <GradeBadge grade={p.confirmedGrade} />
                </td>
                <td className="mono">{num(p.fractalDimension, 4)}</td>
                <td className="mono">{p.hbA1c == null ? "—" : `${p.hbA1c}%`}</td>
              </tr>
            ))}
          </DataTable>
        </LoadState>
      </Panel>
    </>
  );
}

/* -------------------------------------------------- Chỉ số theo thời gian (tình trạng) */
interface MetricRow {
  visitId: number;
  date: string | null;
  hbA1c?: number | null;
  glucose?: number | null;
  systolic?: number | null;
  diastolic?: number | null;
}

function MetricHistoryPanel({
  patientId,
  visits,
}: {
  patientId: number;
  visits: VisitDto[];
}) {
  const data = useData();

  const rows = useAsync(async (): Promise<MetricRow[]> => {
    const out = await Promise.all(
      visits.map(async (v) => {
        try {
          const m = await data.visits.healthMetrics(v.id);
          return {
            visitId: v.id,
            date: v.visitDate,
            hbA1c: m.hbA1c?.value ?? null,
            glucose: m.glucose?.value ?? null,
            // bloodPressure trả về một chỉ số; systolic (type 3) là giá trị chính,
            // diastolic đi kèm nếu backend trả cặp.
            systolic: m.bloodPressure?.metricType === 3 ? m.bloodPressure.value : null,
            diastolic: (m.bloodPressure as { diastolicValue?: number } | null | undefined)?.diastolicValue ?? null,
          } as MetricRow;
        } catch {
          return { visitId: v.id, date: v.visitDate } as MetricRow;
        }
      }),
    );
    return out.sort((a, b) => (a.date || "").localeCompare(b.date || ""));
  }, [patientId, visits.map((v) => v.id).join(",")]);

  const series = useMemo(() => {
    const pts = (pick: (r: MetricRow) => number | null | undefined, name: string, kind?: string) => ({
      name,
      kind,
      points: (rows.data ?? []).map((r) => ({ x: r.date ?? "", y: pick(r) })),
    });
    return [
      pts((r) => r.hbA1c, "HbA1c", "alert"),
      pts((r) => r.systolic, "HA tâm thu", "defer"),
    ];
  }, [rows.data]);

  const hasAny = (rows.data ?? []).some(
    (r) => r.hbA1c != null || r.glucose != null || r.systolic != null,
  );

  return (
    <Panel title="Chỉ số theo thời gian">
      <p className="muted">
        Tổng hợp {metricTypes[2]}, {metricTypes[1]} và huyết áp theo từng lượt khám. Huyết áp cần ≥ 3 lần đo hợp lệ mới góp điểm nguy cơ (chỉ với Type 2).
      </p>
      <LoadState
        loading={rows.loading}
        error={rows.error}
        empty={!hasAny}
        onRetry={rows.reload}
        emptyText="Chưa có chỉ số nào được ghi."
      >
        {hasAny && <LineChart series={series} />}
        <DataTable headers={["Ngày khám", "Lượt", "HbA1c (%)", "Đường huyết", "HA tâm thu", "HA tâm trương"]}>
          {rows.data?.map((r) => (
            <tr key={r.visitId}>
              <td className="mono">{fmtDate(r.date)}</td>
              <td className="mono">#{r.visitId}</td>
              <td className="mono">{r.hbA1c ?? "—"}</td>
              <td className="mono">{r.glucose ?? "—"}</td>
              <td className="mono">{r.systolic ?? "—"}</td>
              <td className="mono">{r.diastolic ?? "—"}</td>
            </tr>
          ))}
        </DataTable>
      </LoadState>
    </Panel>
  );
}
