import { useCallback, useEffect, useMemo, useRef, useState, type DragEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useData } from "@/contexts/DataContext";
import { useToast } from "@/contexts/ToastContext";
import { useAsync } from "@/lib/hooks";
import { Button, DataTable, Field, GradeBadge, LoadState, Panel, StatusBadge } from "@/components/ui";
import { ProtectedImage } from "@/components/ProtectedImage";
import { grades, metricContexts } from "@/lib/enums";
import { gradingApi } from "@/api/services";
import type { AiDiagnosisDto, BlindGradeRequest, ImageClinicalContextDto, PatientDetailDto, VisitDto, VisitHealthMetricsDto } from "@/types/api";
import { BlindForm, BlindReviewDialog } from "@/lite/BlindReview";
import { contextFromVisit } from "@/components/ClinicalContext";
import {
  DR_TEXT,
  EYE_NAME,
  REFERRAL,
  REFERRAL_BY_GRADE,
  bpOf,
  drText,
  finalGrade,
  latestOf,
  loadEntries,
  plainDeferReason,
  type Entry,
} from "@/lite/liteData";
import "@/lite/lite.css";

/* Lượt thu thập đang mở:
   thanh tiến độ → chỉ số (tự lưu) → hai thẻ mắt (thả ảnh → bác sĩ chấm mù → "Lưu phân độ & chạy AI"
   = nạp ảnh + lưu chấm mù + chạy AI → xác nhận lâm sàng trên thẻ) → đóng lượt (kết luận điền sẵn).
   Chỉ gọi lại các API sẵn có; mọi quy tắc nghiệp vụ vẫn nằm ở backend. */

const MAX_BYTES = 10 * 1024 * 1024;
// ProtectedImage mặc định là ảnh thu nhỏ 72×52; thẻ mắt cần ảnh đủ lớn để nhận ra ảnh.
const EYE_IMG = { width: "100%", height: "auto", maxHeight: 280, objectFit: "contain" as const, borderRadius: 4 };
const OTHER = (eye: number) => (eye === 0 ? 1 : 0);

interface EyeStatus {
  text: string;
  kind: "" | "ok" | "watch" | "alert" | "defer";
  blocking: boolean; // chặn đóng lượt
}

function eyeStatus(entry: Entry | null, preview: File | null, busy: boolean): EyeStatus {
  if (busy) return { text: "đang xử lý…", kind: "watch", blocking: true };
  if (!entry) return preview
    ? { text: "chờ chấm mù", kind: "watch", blocking: true }
    : { text: "chưa có ảnh", kind: "", blocking: false };
  const q = entry.img.qualityStatus;
  if (q === 2) return entry.blind && !entry.blind.gradable
    ? { text: "không chấm được", kind: "alert", blocking: false }
    : { text: "ảnh không đạt — cần thay", kind: "alert", blocking: true };
  const dx = entry.dx;
  if (!dx) return entry.blind
    ? { text: "chưa chạy AI", kind: "watch", blocking: true }
    : { text: "chờ chấm mù", kind: "watch", blocking: true };
  if (!dx.isConfirmed) return dx.isDeferred
    ? { text: "cần xem kỹ để xác nhận", kind: "defer", blocking: true }
    : { text: "chờ xác nhận", kind: "watch", blocking: true };
  return { text: `✓ ${drText(finalGrade(entry))}`, kind: "ok", blocking: false };
}

/* ================================================================ workspace */
export function VisitWorkspace({
  patient,
  visit,
  onChanged,
  onClosed,
}: {
  patient: PatientDetailDto;
  visit: VisitDto;
  onChanged: () => void;
  onClosed: () => void;
}) {
  const data = useData();
  const toast = useToast();
  const list = useAsync(() => loadEntries(data, { patientId: patient.id, visitId: visit.id }), [patient.id, visit.id]);
  const [previews, setPreviews] = useState<(File | null)[]>([null, null]);
  const [busy, setBusy] = useState<boolean[]>([false, false]);
  const [metricsSaved, setMetricsSaved] = useState(false);

  const entries = list.data ?? [];
  const eyesEntry = [latestOf(entries, 0), latestOf(entries, 1)];
  const status = [0, 1].map((e) => eyeStatus(eyesEntry[e], previews[e], busy[e]));
  // Ảnh nạp trùng cùng mắt (lỗi bản ≤ 1.3.0: bấm "Lưu phân độ & chạy AI" hai lần). Thẻ mắt chỉ hiện ảnh
  // mới nhất nên các ảnh này bị ẩn nhưng vẫn chặn đóng lượt ở backend. Liệt kê ra để thu hồi.
  const hidden = entries.filter((x) => x !== eyesEntry[x.img.eye]);
  // Kết quả AI trùng của ảnh đang hiện (lỗi deadlock khi hai mắt chạy AI xong cùng lúc, bản 1.3.1 đầu).
  const extraDx = eyesEntry.flatMap((x) => (x ? x.extraDx.map((dx) => ({ entry: x, dx })) : []));

  const setPreview = (eye: number, f: File | null) =>
    setPreviews((p) => (eye === 0 ? [f, p[1]] : [p[0], f]));
  const setEyeBusy = (eye: number, b: boolean) =>
    setBusy((p) => (eye === 0 ? [b, p[1]] : [p[0], b]));

  const refresh = () => {
    list.reload();
    onChanged();
  };

  // Nhận tệp (chọn hoặc kéo thả). Thả 2 ảnh cùng lúc: ảnh thứ hai sang mắt còn trống.
  const onFiles = (eye: number, files: File[]) => {
    const ok = files.filter((f) => {
      if (!/^image\/(jpeg|png)$/.test(f.type)) {
        toast.push(`${f.name}: chỉ nhận JPG/PNG.`, "error");
        return false;
      }
      if (f.size > MAX_BYTES) {
        toast.push(`${f.name}: vượt quá 10 MB.`, "error");
        return false;
      }
      return true;
    });
    if (!ok.length) return;
    setPreview(eye, ok[0]);
    const other = OTHER(eye);
    if (ok.length > 1 && !eyesEntry[other] && !previews[other]) {
      setPreview(other, ok[1]);
      toast.push(`Ảnh thứ hai được gán cho ${EYE_NAME[other]}. Kiểm tra lại trước khi phân tích.`, "info");
    }
  };

  const swap = () => setPreviews((p) => [p[1], p[0]]);

  const blockers = [0, 1]
    .filter((e) => status[e].blocking)
    .map((e) => `${EYE_NAME[e]}: ${status[e].text}`);
  if (hidden.length)
    blockers.push(`Có ${hidden.length} ảnh nạp trùng cần thu hồi (xem mục "Ảnh nạp trùng" ở trên).`);
  if (extraDx.length)
    blockers.push(`Có ${extraDx.length} kết quả AI trùng cần thu hồi (xem mục "Kết quả AI trùng" ở trên).`);
  const warnings: string[] = [];
  if (!eyesEntry[0] && !eyesEntry[1] && !previews[0] && !previews[1]) warnings.push("Lượt chưa có ảnh nào.");
  else [0, 1].forEach((e) => !eyesEntry[e] && !previews[e] && warnings.push(`Chưa có ảnh ${EYE_NAME[e]}.`));

  return (
    <>
      <div className="lite-steps" role="status">
        <b>Tiến độ lượt</b>
        <span className={`lite-step ${metricsSaved ? "ok" : ""}`}>
          {metricsSaved ? "✓ Chỉ số đã lưu" : "Chỉ số: chưa nhập (không bắt buộc)"}
        </span>
        {[0, 1].map((e) => (
          <span key={e} className={`lite-step ${status[e].kind}`}>
            {e === 0 ? "OD" : "OS"}: {status[e].text}
          </span>
        ))}
      </div>

      <MetricsPanel visit={visit} onSavedChange={setMetricsSaved} />

      <Panel title="Ảnh đáy mắt">
        {!list.data ? (
          <LoadState loading={list.loading} error={list.error} onRetry={list.reload}>{null}</LoadState>
        ) : (
          <div className="grid2">
            {[0, 1].map((eye) => (
              <EyeCard
                key={eye}
                eye={eye}
                patient={patient}
                visitDate={visit.visitDate}
                patientId={patient.id}
                visitId={visit.id}
                entry={eyesEntry[eye]}
                preview={previews[eye]}
                otherHasPreview={!!previews[OTHER(eye)]}
                onFiles={(f) => onFiles(eye, f)}
                clearPreview={() => setPreview(eye, null)}
                swap={swap}
                setBusy={(b) => setEyeBusy(eye, b)}
                onChanged={refresh}
              />
            ))}
          </div>
        )}
      </Panel>

      {hidden.length > 0 && <DuplicatePanel items={hidden} onChanged={refresh} />}
      {extraDx.length > 0 && <DuplicateDxPanel items={extraDx} onChanged={refresh} />}

      <ClosePanel visit={visit} entries={eyesEntry} blockers={blockers} warnings={warnings} onClosed={onClosed} />
    </>
  );
}

/* ================================================================ chỉ số */
const RANGES = {
  hba1c: [3, 20],
  sys: [60, 260],
  dia: [30, 160],
  glu: [1, 40],
} as const;

type MetricForm = { hba1c: string; sys: string; dia: string; glu: string; ctx: string };
const EMPTY_FORM: MetricForm = { hba1c: "", sys: "", dia: "", glu: "", ctx: "" };

const toForm = (m: VisitHealthMetricsDto | null): MetricForm => {
  const s = (v?: number | null) => (v == null ? "" : String(v));
  const bp = bpOf(m?.bloodPressure);
  return {
    hba1c: s(m?.hbA1c?.value),
    glu: s(m?.glucose?.value),
    ctx: s(m?.glucose?.context),
    sys: s(bp.sys),
    dia: s(bp.dia),
  };
};

const sameForm = (a: MetricForm, b: MetricForm) =>
  (Object.keys(a) as (keyof MetricForm)[]).every((k) => a[k].trim() === b[k].trim());

function MetricsPanel({ visit, onSavedChange }: { visit: VisitDto; onSavedChange: (v: boolean) => void }) {
  const data = useData();
  const metrics = useAsync(() => data.visits.healthMetrics(visit.id), [visit.id]);
  const [form, setForm] = useState<MetricForm>(EMPTY_FORM);
  const [saved, setSaved] = useState<MetricForm>(EMPTY_FORM);
  const [state, setState] = useState<{ kind: "idle" | "saving" | "saved" | "pending" | "error"; msg?: string }>({ kind: "idle" });
  const formRef = useRef(form);
  formRef.current = form;

  useEffect(() => {
    if (!metrics.data) return;
    const f = toForm(metrics.data);
    setForm(f);
    setSaved(f);
    onSavedChange(Object.values(f).some((v) => v !== ""));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [metrics.data]);

  const patch = (k: keyof MetricForm, v: string) => setForm((f) => ({ ...f, [k]: v }));

  const parse = (v: string): number | null => {
    if (!v.trim()) return null;
    const n = Number(v.replace(",", "."));
    return Number.isFinite(n) ? n : NaN;
  };

  const save = async (f: MetricForm = formRef.current) => {
    if (sameForm(f, saved)) return;
    const h = parse(f.hba1c), g = parse(f.glu), s = parse(f.sys), d = parse(f.dia);
    if ([h, g, s, d].some((x) => x != null && Number.isNaN(x)))
      return setState({ kind: "pending", msg: "Chưa lưu: có giá trị không phải số." });
    if (g != null && !f.ctx) return setState({ kind: "pending", msg: "Chưa lưu: chọn thời điểm đo đường huyết." });
    if ((s == null) !== (d == null)) return setState({ kind: "pending", msg: "Chưa lưu: nhập đủ tâm thu và tâm trương." });
    if (s != null && d != null && s <= d) return setState({ kind: "pending", msg: "Chưa lưu: tâm thu phải lớn hơn tâm trương." });

    setState({ kind: "saving" });
    try {
      const cur = metrics.data;
      const bp = cur?.bloodPressure;
      const res = await data.visits.saveHealthMetrics(visit.id, {
        glucose: g,
        glucoseContext: g == null ? null : Number(f.ctx),
        glucoseNote: null,
        glucoseRowVersion: cur?.glucose?.rowVersion ?? null,
        hbA1c: h,
        hbA1cNote: null,
        hbA1cRowVersion: cur?.hbA1c?.rowVersion ?? null,
        systolicBp: s,
        diastolicBp: d,
        bloodPressureNote: null,
        systolicRowVersion: bp?.metricType === 3 ? bp.rowVersion : bp?.pairRowVersion ?? null,
        diastolicRowVersion: bp?.metricType === 4 ? bp.rowVersion : bp?.pairRowVersion ?? null,
      });
      metrics.setData(res); // rowVersion mới cho lần lưu sau
      setState({ kind: "saved" });
    } catch (e) {
      setState({ kind: "error", msg: (e as Error).message });
      metrics.reload(); // rowVersion có thể đã cũ
    }
  };

  const warn = (k: keyof typeof RANGES, v: string) => {
    const n = Number(v);
    if (!v.trim() || !Number.isFinite(n)) return undefined;
    const [lo, hi] = RANGES[k];
    return n < lo || n > hi ? `Ngoài khoảng thường gặp (${lo}–${hi}), kiểm tra lại.` : undefined;
  };

  const numInput = (k: keyof MetricForm, step?: string) => (
    <input
      type="number"
      inputMode="decimal"
      step={step}
      value={form[k]}
      onChange={(e) => patch(k, e.target.value)}
      onBlur={() => save()}
    />
  );

  const badge =
    state.kind === "saving" ? <span className="lite-save">Đang lưu…</span>
    : state.kind === "saved" ? <span className="lite-save ok">✓ Đã lưu</span>
    : state.kind === "pending" ? <span className="lite-save watch">{state.msg}</span>
    : state.kind === "error" ? (
      <span className="lite-save alert">
        Lỗi lưu: {state.msg} <button className="lite-linkbtn" onClick={() => save()}>Thử lại</button>
      </span>
    ) : <span className="lite-save">Tự lưu khi rời ô nhập</span>;

  return (
    <Panel title="Chỉ số lâm sàng" action={badge}>
      {!metrics.data ? (
        <LoadState loading={metrics.loading} error={metrics.error} onRetry={metrics.reload}>{null}</LoadState>
      ) : (
        <>
          <p className="muted">HbA1c và huyết áp được dùng để tính điểm nguy cơ. Để trống nếu không đo.</p>
          <div className="form-row three">
            <Field labelText="HbA1c (%)" className={warn("hba1c", form.hba1c) ? "warn" : ""} help={warn("hba1c", form.hba1c)}>
              {numInput("hba1c", "0.1")}
            </Field>
            <Field labelText="Huyết áp tâm thu (mmHg)" className={warn("sys", form.sys) ? "warn" : ""} help={warn("sys", form.sys)}>
              {numInput("sys")}
            </Field>
            <Field labelText="Huyết áp tâm trương (mmHg)" className={warn("dia", form.dia) ? "warn" : ""} help={warn("dia", form.dia)}>
              {numInput("dia")}
            </Field>
            <Field labelText="Đường huyết (mmol/L)" className={warn("glu", form.glu) ? "warn" : ""} help={warn("glu", form.glu)}>
              {numInput("glu", "0.1")}
            </Field>
            <Field labelText="Thời điểm đo đường huyết">
              <select
                value={form.ctx}
                onChange={(e) => {
                  const f = { ...formRef.current, ctx: e.target.value };
                  setForm(f);
                  save(f);
                }}
              >
                {metricContexts.map((c, i) => (
                  <option key={i} value={i === 0 ? "" : String(i)}>{c || "— chọn —"}</option>
                ))}
              </select>
            </Field>
          </div>
        </>
      )}
    </Panel>
  );
}

/* ================================================================ thẻ mắt */
/* Luồng một mắt (bản nghiên cứu):
   thả ảnh → "Xem ảnh & chấm mù" (khung xem phóng to/kéo thả + thông tin lâm sàng)
   → bác sĩ CHẤM MÙ (chưa thấy AI) → "Lưu phân độ & chạy AI"
   = nạp ảnh + lưu chấm mù (vòng 1) + chạy AI → xác nhận lâm sàng trên thẻ.
   "Không chấm được" = ảnh không đạt, AI không chạy. Phân độ chấm mù không sửa được. */
export { BlindForm };

function EyeCard({
  eye,
  patient,
  visitDate,
  patientId,
  visitId,
  entry,
  preview,
  otherHasPreview,
  onFiles,
  clearPreview,
  swap,
  setBusy,
  onChanged,
}: {
  eye: number;
  patient: PatientDetailDto;
  visitDate: string;
  patientId: number;
  visitId: number;
  entry: Entry | null;
  preview: File | null;
  otherHasPreview: boolean;
  onFiles: (f: File[]) => void;
  clearPreview: () => void;
  swap: () => void;
  setBusy: (b: boolean) => void;
  onChanged: () => void;
}) {
  const data = useData();
  const toast = useToast();
  const navigate = useNavigate();
  const fileRef = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);
  const [work, setWork] = useState<"" | "analyze" | "approve" | "override" | "void">("");
  const [phase, setPhase] = useState("");
  const [startedAt, setStartedAt] = useState<number | null>(null);
  const [elapsed, setElapsed] = useState(0);
  const [error, setError] = useState("");
  const [overriding, setOverriding] = useState(false);
  const [grade, setGrade] = useState(0);
  const [reason, setReason] = useState("");
  // Màn xem ảnh & chấm mù (vòng 1). ctx = null: tải theo ảnh đã lưu.
  const [review, setReview] = useState<{ ctx: ImageClinicalContextDto | null } | null>(null);

  const previewUrl = useMemo(() => (preview ? URL.createObjectURL(preview) : ""), [preview]);
  useEffect(() => () => { if (previewUrl) URL.revokeObjectURL(previewUrl); }, [previewUrl]);

  useEffect(() => {
    if (startedAt == null) return;
    const t = setInterval(() => setElapsed(Math.floor((Date.now() - startedAt) / 1000)), 1000);
    return () => clearInterval(t);
  }, [startedAt]);

  const img = entry?.img ?? null;
  const dx = entry?.dx ?? null;
  const blind = entry?.blind ?? null;
  const name = EYE_NAME[eye];
  const openDetail = () => img && navigate(dx ? `/fundus/${img.id}?diagnosis=${dx.id}` : `/fundus/${img.id}`);

  /* nạp ảnh (nếu chưa) → lưu chấm mù (nếu chưa) → chạy AI (nếu chấm được).
     Lỗi ở bước nào thì dừng ở đó; bước đã xong không làm lại. */
  // Chặn chạy chồng: bấm hai lần (hoặc Enter hai lần) trước khi giao diện kịp khóa từng làm nạp hai ảnh.
  const inFlight = useRef(false);
  const analyze = async (req?: BlindGradeRequest) => {
    if (inFlight.current) return;
    inFlight.current = true;
    setError("");
    setWork("analyze");
    setBusy(true);
    setStartedAt(Date.now());
    setElapsed(0);
    let step = "Nạp ảnh";
    try {
      let cur = img;
      if (!cur) {
        if (!preview) return;
        setPhase("Đang nạp ảnh…");
        cur = await data.images.upload(preview, patientId, visitId, eye);
        clearPreview();
      }
      let gradable = blind?.gradable ?? true;
      if (!blind) {
        if (!req) throw new Error("Chưa có phân độ chấm mù.");
        step = "Lưu chấm mù";
        setPhase("Đang lưu chấm mù…");
        await gradingApi.submitRound1(cur.id, req);
        gradable = req.gradable;
      }
      if (gradable) {
        step = "Phân tích AI";
        setPhase("Đang phân tích AI…");
        await data.diagnoses.run(cur.id);
        toast.push(`Đã phân tích ${name}.`, "success");
      } else {
        toast.push(`Đã ghi ${name}: không chấm được.`, "success");
      }
    } catch (e) {
      setError(`${step} thất bại: ${(e as Error).message}`);
    } finally {
      inFlight.current = false;
      setWork("");
      setPhase("");
      setBusy(false);
      setStartedAt(null);
      onChanged();
    }
  };

  const openReview = async () => {
    if (img) return setReview({ ctx: null });
    const m = await data.visits.healthMetrics(visitId).catch(() => null);
    setReview({ ctx: contextFromVisit(patient, m, eye, visitDate) });
  };
  const closeReview = useCallback(() => setReview(null), []);
  const submitReview = useCallback((r: BlindGradeRequest) => {
    setReview(null);
    analyze(r);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [img, preview, blind]);

  const act = async (key: typeof work, fn: () => Promise<unknown>, ok: string) => {
    setError("");
    setWork(key);
    try {
      await fn();
      toast.push(ok, "success");
      setOverriding(false);
      setReason("");
      onChanged();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setWork("");
    }
  };

  const approve = () => dx && act("approve", () => data.triage.approve(dx.id, dx.rowVersion), `Đã xác nhận ${name}.`);
  const override = () => {
    if (!dx) return;
    if (!reason.trim()) return setError("Nhập lý do khi chọn phân độ khác AI.");
    act("override", () => data.triage.override(dx.id, { rowVersion: dx.rowVersion, finalGrade: grade, reason: reason.trim() }), `Đã ghi phân độ ${name}.`);
  };
  const replace = () => {
    if (!img || !window.confirm(`Thu hồi ảnh ${name} và kết quả liên quan để nạp ảnh khác?`)) return;
    act("void", () => data.images.void(img.id, "Thay ảnh (nạp nhầm hoặc chất lượng kém)", img.rowVersion), "Đã thu hồi ảnh.");
  };

  const pick = (files: FileList | null) => {
    if (files?.length) onFiles(Array.from(files));
    if (fileRef.current) fileRef.current.value = "";
  };
  const drop = (e: DragEvent) => {
    e.preventDefault();
    setOver(false);
    pick(e.dataTransfer.files);
  };

  const running = work === "analyze";
  const mm = `${Math.floor(elapsed / 60)}:${String(elapsed % 60).padStart(2, "0")}`;
  const blindLine = blind && (
    <div className="lite-note">
      Chấm mù: <b>{blind.gradable ? drText(blind.grade) : "không chấm được"}</b>
      {blind.note && <span className="faint"> · {blind.note}</span>}
    </div>
  );

  let body: JSX.Element;
  if (running) {
    body = (
      <>
        {previewUrl ? <img className="lite-eye-img" src={previewUrl} alt={name} /> : img && <ProtectedImage imageId={img.id} className="lite-eye-img" style={EYE_IMG} alt={name} />}
        <div className="lite-note">{phase || "Đang xử lý…"} <span className="mono">{mm}</span> (máy CPU có thể mất vài phút)</div>
      </>
    );
  } else if (!img && !preview) {
    body = (
      <div
        className={`lite-drop ${over ? "over" : ""}`}
        onDragOver={(e) => { e.preventDefault(); setOver(true); }}
        onDragLeave={() => setOver(false)}
        onDrop={drop}
        onClick={() => fileRef.current?.click()}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && fileRef.current?.click()}
      >
        <b>Kéo thả ảnh {eye === 0 ? "OD" : "OS"} vào đây</b>
        <span>hoặc bấm để chọn · JPG/PNG, tối đa 10 MB</span>
        <input ref={fileRef} type="file" accept="image/jpeg,image/png" multiple hidden onChange={(e) => pick(e.target.files)} />
      </div>
    );
  } else if (!img && preview) {
    body = (
      <>
        <img className="lite-eye-img clickable" src={previewUrl} alt={`Xem trước ${name}`} onClick={openReview} />
        <div className="faint lite-fname" title={preview.name}>{preview.name}</div>
        <div className="lite-actions">
          <Button kind="primary" onClick={openReview}>Xem ảnh &amp; chấm mù →</Button>
          <button className="lite-linkbtn" onClick={clearPreview}>Chọn ảnh khác</button>
          <button className="lite-linkbtn" onClick={swap}>{otherHasPreview ? "Đổi ảnh hai mắt" : `Chuyển sang ${eye === 0 ? "OS" : "OD"}`}</button>
        </div>
      </>
    );
  } else if (img && img.qualityStatus === 2) {
    body = (
      <>
        <ProtectedImage imageId={img.id} className="lite-eye-img" style={EYE_IMG} alt={name} onClick={openDetail} />
        {blindLine}
        <div className="lite-note error">
          {blind && !blind.gradable
            ? "Bác sĩ đánh giá ảnh không chấm được; AI không chạy. Có thể chụp lại và nạp ảnh khác."
            : "Ảnh không đạt chất lượng. Thay ảnh khác để phân tích."}
        </div>
        <div className="lite-actions">
          <Button busy={work === "void"} onClick={replace}>Thay ảnh</Button>
        </div>
      </>
    );
  } else if (img && !dx && !blind) {
    body = (
      <>
        <ProtectedImage imageId={img.id} className="lite-eye-img" style={EYE_IMG} alt={name} onClick={openReview} />
        <div className="lite-actions">
          <Button kind="primary" onClick={openReview}>Xem ảnh &amp; chấm mù →</Button>
          <button className="lite-linkbtn" disabled={work === "void"} onClick={replace}>Thay ảnh</button>
        </div>
      </>
    );
  } else if (img && !dx) {
    body = (
      <>
        <ProtectedImage imageId={img.id} className="lite-eye-img" style={EYE_IMG} alt={name} onClick={openDetail} />
        {blindLine}
        <div className="lite-actions">
          <Button kind="primary" onClick={() => analyze()}>Chạy AI</Button>
          <Button busy={work === "void"} onClick={replace}>Thay ảnh</Button>
        </div>
      </>
    );
  } else {
    const confirmed = dx!.isConfirmed && dx!.review;
    body = (
      <>
        <ProtectedImage imageId={img!.id} className="lite-eye-img" style={EYE_IMG} alt={name} onClick={openDetail} />
        {blindLine ?? <div className="lite-note faint">Ảnh này không có chấm mù (nạp trước bản 1.2).</div>}
        <div className="lite-result">
          <span className="faint">AI đề xuất</span> <GradeBadge grade={dx!.drGrade} />
        </div>

        {confirmed ? (
          <>
            <div className="lite-note ok">
              ✓ Quyết định lâm sàng: <b>{drText(dx!.review!.finalGrade)}</b> ({dx!.review!.actionLabel})
            </div>
            <div className="lite-actions">
              <Button onClick={openDetail}>Xem kỹ →</Button>
            </div>
          </>
        ) : dx!.isDeferred ? (
          <>
            <div className="lite-note defer">⚠ Cần xem kỹ: {plainDeferReason(dx!)}</div>
            <div className="lite-actions">
              <Button kind="primary" onClick={openDetail}>Xem kỹ để xác nhận →</Button>
              <Button busy={work === "void"} onClick={replace}>Thay ảnh</Button>
            </div>
          </>
        ) : overriding ? (
          <div className="lite-override">
            <Field labelText="Phân độ của bác sĩ" required>
              <select value={grade} onChange={(e) => setGrade(Number(e.target.value))}>
                {grades.map((g, i) => (
                  <option key={i} value={i} disabled={i === dx!.drGrade}>{DR_TEXT[i] ?? g}</option>
                ))}
              </select>
            </Field>
            <Field labelText="Lý do" required>
              <textarea rows={2} value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
            </Field>
            <div className="lite-actions">
              <Button kind="primary" busy={work === "override"} onClick={override}>Lưu phân độ</Button>
              <Button onClick={() => setOverriding(false)}>Hủy</Button>
            </div>
          </div>
        ) : (
          <div className="lite-actions">
            <Button kind="primary" busy={work === "approve"} onClick={approve}>
              ✓ Đồng ý: {drText(dx!.drGrade)}
            </Button>
            <Button onClick={() => { setGrade(dx!.drGrade === 0 ? 1 : 0); setOverriding(true); }}>Chọn độ khác</Button>
            <Button onClick={openDetail}>Xem kỹ →</Button>
            <button className="lite-linkbtn" disabled={work === "void"} onClick={replace}>Thay ảnh</button>
          </div>
        )}
      </>
    );
  }

  const st = eyeStatus(entry, preview, running);
  return (
    <div className="panel lite-eye" style={{ margin: 0 }}>
      <div className="panel-h">
        <span>{name}</span>
        <StatusBadge text={st.text} kind={st.kind} />
      </div>
      <div className="panel-b">
        {body}
        {error && <div className="lite-note error">{error}</div>}
      </div>
      {review && (preview || img) && (
        <BlindReviewDialog
          title={`Chấm mù ${name}`}
          source={img ? { kind: "image", imageId: img.id } : { kind: "file", file: preview! }}
          context={review.ctx}
          onSubmit={submitReview}
          onClose={closeReview}
        />
      )}
    </div>
  );
}

/* ================================================================ ảnh nạp trùng */
/* Ảnh cũ cùng mắt còn hiệu lực. Thu hồi = lan sang kết quả AI và xác nhận của ảnh đó, nên lượt đóng được
   và bảng chấm mù / dataset không còn dòng trùng. Ảnh giữ lại là ảnh đang hiện trên thẻ mắt. */
function DuplicatePanel({ items, onChanged }: { items: Entry[]; onChanged: () => void }) {
  const data = useData();
  const toast = useToast();
  const navigate = useNavigate();
  const [busyId, setBusyId] = useState<number | null>(null);

  const remove = async (x: Entry) => {
    if (!window.confirm(`Thu hồi ảnh nạp trùng #${x.img.id} (${EYE_NAME[x.img.eye]}) và kết quả AI của ảnh đó?`)) return;
    setBusyId(x.img.id);
    try {
      await data.images.void(x.img.id, "Ảnh nạp trùng cùng mắt (lỗi bấm hai lần, bản ≤ 1.3.0)", x.img.rowVersion);
      toast.push(`Đã thu hồi ảnh #${x.img.id}.`, "success");
      onChanged();
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusyId(null);
    }
  };

  return (
    <Panel title="Ảnh nạp trùng">
      <div className="lite-note error">
        Mỗi mắt chỉ giữ một ảnh. Các ảnh dưới đây là bản nạp trùng (ảnh đang hiện trên thẻ mắt được giữ lại).
        Thu hồi chúng trước khi đóng lượt.
      </div>
      <DataTable headers={["Ảnh", "Mắt", "Nạp lúc", "Trạng thái", ""]}>
          {items.map((x) => (
            <tr key={x.img.id}>
              <td className="mono">#{x.img.id}</td>
              <td>{EYE_NAME[x.img.eye]}</td>
              <td className="mono">{new Date(x.img.createdAt).toLocaleString("vi-VN")}</td>
              <td>{eyeStatus(x, null, false).text}</td>
              <td className="lite-actions">
                <Button onClick={() => navigate(`/fundus/${x.img.id}`)}>Xem</Button>
                <Button busy={busyId === x.img.id} onClick={() => remove(x)}>Thu hồi</Button>
              </td>
            </tr>
          ))}
      </DataTable>
    </Panel>
  );
}

/* ================================================================ kết quả AI trùng */
/* Một ảnh có hơn một kết quả AI còn hiệu lực: thẻ mắt hiện kết quả mới nhất, các bản cũ hơn liệt kê ở đây.
   Hai bản cùng một lần chạy AI nên nội dung như nhau; thu hồi bản cũ (lan sang xác nhận của bản đó). */
function DuplicateDxPanel({ items, onChanged }: { items: { entry: Entry; dx: AiDiagnosisDto }[]; onChanged: () => void }) {
  const data = useData();
  const toast = useToast();
  const navigate = useNavigate();
  const [busyId, setBusyId] = useState<number | null>(null);

  const remove = async (dx: AiDiagnosisDto, eye: number) => {
    if (!window.confirm(`Thu hồi kết quả AI trùng #${dx.id} của ${EYE_NAME[eye]}? Kết quả đang hiện trên thẻ mắt được giữ lại.`)) return;
    setBusyId(dx.id);
    try {
      await data.diagnoses.void(dx.id, "Kết quả AI trùng (hai mắt chạy AI cùng lúc, lỗi bản 1.3.1 đầu)", dx.rowVersion ?? "");
      toast.push(`Đã thu hồi kết quả AI #${dx.id}.`, "success");
      onChanged();
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusyId(null);
    }
  };

  return (
    <Panel title="Kết quả AI trùng">
      <div className="lite-note error">
        Mỗi ảnh chỉ giữ một kết quả AI. Các kết quả dưới đây là bản ghi thừa của cùng ảnh (kết quả đang hiện trên
        thẻ mắt được giữ lại). Thu hồi chúng trước khi đóng lượt.
      </div>
      <DataTable headers={["Kết quả", "Ảnh", "Mắt", "Lúc chạy", "AI", "Trạng thái", ""]}>
        {items.map(({ entry, dx }) => (
          <tr key={dx.id}>
            <td className="mono">#{dx.id}</td>
            <td className="mono">#{entry.img.id}</td>
            <td>{EYE_NAME[entry.img.eye]}</td>
            <td className="mono">{new Date(dx.createdAt).toLocaleString("vi-VN")}</td>
            <td>{drText(dx.drGrade)}</td>
            <td>{dx.isConfirmed ? "đã xác nhận" : "chưa xác nhận"}</td>
            <td className="lite-actions">
              <Button onClick={() => navigate(`/fundus/${entry.img.id}?diagnosis=${dx.id}`)}>Xem</Button>
              <Button busy={busyId === dx.id} onClick={() => remove(dx, entry.img.eye)}>Thu hồi</Button>
            </td>
          </tr>
        ))}
      </DataTable>
    </Panel>
  );
}

/* ================================================================ đóng lượt */
function ClosePanel({
  visit,
  entries,
  blockers,
  warnings,
  onClosed,
}: {
  visit: VisitDto;
  entries: (Entry | null)[];
  blockers: string[];
  warnings: string[];
  onClosed: () => void;
}) {
  const data = useData();
  const toast = useToast();
  const g = entries.map(finalGrade);
  const auto = [0, 1]
    .filter((e) => g[e] != null)
    .map((e) => `${e === 0 ? "Mắt phải" : "Mắt trái"}: ${drText(g[e]).toLowerCase()}`)
    .join(" · ");
  const maxG = Math.max(-1, ...g.filter((x): x is number => x != null));
  const autoReferral = maxG >= 0 ? REFERRAL_BY_GRADE[maxG] : 0;

  const [conclusion, setConclusion] = useState("");
  const [conclusionTouched, setConclusionTouched] = useState(false);
  const [referral, setReferral] = useState(0);
  const [referralTouched, setReferralTouched] = useState(false);
  const [months, setMonths] = useState<number | "">("");
  const [busy, setBusy] = useState(false);

  const shownConclusion = conclusionTouched ? conclusion : auto;
  const shownReferral = referralTouched ? referral : autoReferral;

  const close = async () => {
    if (!shownConclusion.trim()) return toast.push("Kết luận là bắt buộc khi đóng lượt.", "error");
    setBusy(true);
    try {
      await data.visits.close(visit.id, {
        conclusion: shownConclusion.trim(),
        referral: shownReferral,
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
    <Panel title="Đóng lượt">
      <Field labelText="Kết luận" required help={conclusionTouched ? undefined : "Điền sẵn từ phân độ đã xác nhận, sửa nếu cần."}>
        <textarea
          rows={2}
          value={shownConclusion}
          maxLength={1000}
          onChange={(e) => { setConclusionTouched(true); setConclusion(e.target.value); }}
        />
      </Field>
      <div className="form-row two">
        <Field labelText="Chuyển tuyến" help={referralTouched || maxG < 0 ? undefined : "Gợi ý theo mức DR cao nhất."}>
          <select value={shownReferral} onChange={(e) => { setReferralTouched(true); setReferral(Number(e.target.value)); }}>
            {REFERRAL.map((r, i) => (
              <option key={i} value={i}>{r}</option>
            ))}
          </select>
        </Field>
        <Field labelText="Tái khám sau (tháng)" help="Để trống: hệ thống tự tính theo mức DR đã xác nhận.">
          <input type="number" min={1} max={24} value={months} onChange={(e) => setMonths(e.target.value ? Number(e.target.value) : "")} />
        </Field>
      </div>
      {blockers.length > 0 && (
        <ul className="lite-blockers">
          {blockers.map((b) => <li key={b}>{b}</li>)}
        </ul>
      )}
      {blockers.length === 0 && warnings.length > 0 && (
        <ul className="lite-blockers faint">
          {warnings.map((w) => <li key={w}>{w} Vẫn có thể đóng lượt.</li>)}
        </ul>
      )}
      <Button kind="primary" busy={busy} disabled={blockers.length > 0} onClick={close}>
        Đóng lượt
      </Button>
    </Panel>
  );
}
