import { useRef, useState } from "react";
import { gradingApi, gradingPackagesApi } from "@/api/services";
import { useAsync } from "@/lib/hooks";
import { useToast } from "@/contexts/ToastContext";
import { Button, DataTable, Field, LoadState, PageHeader, Panel } from "@/components/ui";
import { ProgressPanel } from "@/lite/LiteGrading";
import { saveBlob } from "@/lite/liteData";
import type { GradingImportResultDto, GradingPackageDto } from "@/types/api";
import "@/lite/lite.css";

/* 1.3.1: chấm mù trên nhiều máy, không cần mạng (vai trò Nghiên cứu).
   Máy 1 (máy này): bác sĩ khám chấm vòng 1 và chạy AI. Ở đây người quản lý tạo gói vòng 2 cho máy 2
   và gói vòng 3 (phân xử) cho máy 3. Gói là tệp zip: ảnh đã bỏ metadata, đặt tên ngẫu nhiên, cùng trang chấm
   mở bằng Chrome/Edge. Người chấm xuất tệp KET-QUA-….json, mang về và nhập ở mục "Nhập kết quả". */

const ROUND_TEXT: Record<number, string> = { 2: "Vòng 2 · chấm độc lập", 3: "Vòng 3 · phân xử" };
const STATUS_TEXT = ["Đang chấm", "Đã nhập đủ", "Đã hủy"];
const fmt = (s?: string | null) => (s ? new Date(s).toLocaleString("vi-VN") : "");

export function LitePackagesPage() {
  const toast = useToast();
  const packages = useAsync(() => gradingPackagesApi.list(), []);
  const graders = useAsync(() => gradingPackagesApi.graders(), []);
  const progress = useAsync(() => gradingApi.progress(), []);
  const reloadAll = () => { packages.reload(); graders.reload(); progress.reload(); };

  const [round, setRound] = useState(2);
  const [graderId, setGraderId] = useState<number | "">("");
  const [max, setMax] = useState<number | "">(150);
  const [note, setNote] = useState("");
  const [creating, setCreating] = useState(false);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [importing, setImporting] = useState(false);
  const [result, setResult] = useState<GradingImportResultDto | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const grader = graders.data?.find((g) => g.id === graderId);
  const available = grader ? (round === 2 ? grader.round2Available : grader.round3Available) : null;

  const download = async (p: Pick<GradingPackageDto, "id" | "code">) => {
    setBusyId(p.id);
    try {
      saveBlob(await gradingPackagesApi.download(p.id), `GOI-CHAM-${p.code}.zip`);
      toast.push(`Đã tải gói ${p.code}. Chép tệp zip sang máy chấm.`, "success");
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusyId(null);
    }
  };

  const create = async () => {
    if (graderId === "") return toast.push("Chọn người chấm.", "error");
    setCreating(true);
    try {
      const p = await gradingPackagesApi.create({
        round,
        graderId,
        maxImages: max === "" ? null : Number(max),
        note: note.trim() || null,
      });
      toast.push(`Đã tạo gói ${p.code} (${p.itemCount} ảnh). Đang tải về…`, "success");
      setNote("");
      reloadAll();
      await download(p);
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setCreating(false);
    }
  };

  const cancel = async (p: GradingPackageDto) => {
    const left = p.itemCount - p.importedCount;
    if (!window.confirm(`Hủy gói ${p.code}? ${left} ảnh chưa có kết quả sẽ trở lại hàng chờ. Phân độ đã nhập được giữ nguyên.`)) return;
    setBusyId(p.id);
    try {
      await gradingPackagesApi.cancel(p.id);
      toast.push(`Đã hủy gói ${p.code}.`, "success");
      reloadAll();
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusyId(null);
    }
  };

  const importFile = async (f: File | undefined) => {
    if (!f) return;
    setImporting(true);
    setResult(null);
    try {
      const r = await gradingPackagesApi.importResult(f);
      setResult(r);
      toast.push(`Gói ${r.packageCode}: nhập ${r.imported} phân độ mới.`, r.conflicts.length || r.invalid.length ? "info" : "success");
      reloadAll();
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setImporting(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  };

  return (
    <>
      <PageHeader
        title="Chấm mù trên nhiều máy"
        subtitle="Máy này giữ dữ liệu (vòng 1 và AI). Tạo gói ảnh cho máy chấm vòng 2 và máy phân xử vòng 3; nhận lại tệp kết quả và nhập vào đây."
      />
      {progress.data && <ProgressPanel p={progress.data} />}

      <Panel title="1. Tạo gói chấm">
        <p className="muted">
          Gói gồm ảnh đáy mắt (đã bỏ metadata, đặt tên ngẫu nhiên, xáo thứ tự), thông tin lâm sàng ẩn danh như trang
          chấm trong ứng dụng, và trang chấm. Không có mã bệnh nhân, kết quả AI hay phân độ vòng trước. Mỗi gói dành cho <b>một</b> người chấm; người chấm cần
          có tài khoản vai trò "Người chấm độc lập" trên máy này (Start Menu &gt; DR AI-Diagnosis &gt; Thêm người chấm độc lập).
        </p>
        <div className="form-row two">
          <Field labelText="Vòng chấm" required>
            <select value={round} onChange={(e) => setRound(Number(e.target.value))}>
              <option value={2}>Vòng 2: chấm độc lập (máy 2)</option>
              <option value={3}>Vòng 3: phân xử ảnh bất đồng (máy 3)</option>
            </select>
          </Field>
          <Field
            labelText="Người chấm"
            required
            help={available == null ? undefined : `Còn ${available} ảnh người này được chấm ở vòng ${round}.`}
          >
            <select value={graderId} onChange={(e) => setGraderId(e.target.value ? Number(e.target.value) : "")}>
              <option value="">Chọn người chấm</option>
              {(graders.data ?? []).map((g) => (
                <option key={g.id} value={g.id}>
                  {g.fullName}{g.email ? ` (${g.email})` : ""}
                </option>
              ))}
            </select>
          </Field>
          <Field labelText="Số ảnh tối đa mỗi gói" help="Mặc định 150 (khoảng 200–500 MB). Tối đa 500.">
            <input
              type="number"
              min={1}
              max={500}
              value={max}
              onChange={(e) => setMax(e.target.value ? Number(e.target.value) : "")}
            />
          </Field>
          <Field labelText="Ghi chú (chỉ lưu trên máy này)">
            <input maxLength={500} value={note} onChange={(e) => setNote(e.target.value)} />
          </Field>
        </div>
        {graders.data && graders.data.length === 0 && (
          <div className="lite-note error">Chưa có tài khoản người chấm. Thêm bằng Start Menu &gt; DR AI-Diagnosis &gt; Thêm người chấm độc lập.</div>
        )}
        <div className="lite-actions">
          <Button kind="primary" busy={creating} busyText="Đang tạo gói…" disabled={graderId === "" || available === 0} onClick={create}>
            Tạo gói và tải về
          </Button>
        </div>
      </Panel>

      <Panel title="2. Nhập kết quả từ máy chấm">
        <p className="muted">
          Chọn tệp <span className="mono">KET-QUA-….json</span> người chấm gửi về. Nhập lại cùng tệp hoặc tệp xuất sau
          (đã chấm thêm) không ghi trùng: chỉ phần mới được nhập. Phân độ đã nhập không sửa được.
        </p>
        <input ref={fileRef} type="file" accept=".json,application/json" hidden onChange={(e) => importFile(e.target.files?.[0])} />
        <div className="lite-actions">
          <Button kind="primary" busy={importing} busyText="Đang nhập…" onClick={() => fileRef.current?.click()}>
            Chọn tệp kết quả…
          </Button>
        </div>
        {result && <ImportResult r={result} />}
      </Panel>

      <Panel title="3. Các gói đã tạo">
        <LoadState
          loading={packages.loading && !packages.data}
          error={packages.error}
          empty={!!packages.data && packages.data.length === 0}
          emptyText="Chưa có gói nào."
          onRetry={packages.reload}
        >
          <DataTable headers={["Mã gói", "Vòng", "Người chấm", "Tạo lúc", "Đã nhập", "Trạng thái", ""]}>
            {(packages.data ?? []).map((p) => (
              <tr key={p.id}>
                <td className="mono">{p.code}</td>
                <td>{ROUND_TEXT[p.round] ?? p.round}</td>
                <td>{p.graderName}</td>
                <td className="mono">{fmt(p.createdAt)}</td>
                <td className="mono">{p.importedCount}/{p.itemCount}</td>
                <td title={p.note ?? undefined}>{STATUS_TEXT[p.status] ?? p.status}</td>
                <td className="lite-actions">
                  {p.status !== 2 && (
                    <Button busy={busyId === p.id} onClick={() => download(p)}>Tải lại</Button>
                  )}
                  {p.status === 0 && (
                    <Button busy={busyId === p.id} onClick={() => cancel(p)}>Hủy gói</Button>
                  )}
                </td>
              </tr>
            ))}
          </DataTable>
        </LoadState>
      </Panel>
    </>
  );
}

function ImportResult({ r }: { r: GradingImportResultDto }) {
  const issues = [
    ...r.conflicts.map((x) => ({ ...x, kind: "Không ghi" })),
    ...r.invalid.map((x) => ({ ...x, kind: "Không hợp lệ" })),
  ];
  return (
    <div className="lite-import">
      <div className={`lite-note ${issues.length ? "watch" : "ok"}`}>
        Gói <b className="mono">{r.packageCode}</b> ({ROUND_TEXT[r.round] ?? r.round}): tệp có {r.itemsInFile} phân độ ·
        nhập mới <b>{r.imported}</b> · đã có từ trước {r.unchanged} · không ghi {r.conflicts.length} · không hợp lệ {r.invalid.length}.
        {" "}
        {r.packageStatus === 1 ? "Gói đã nhập đủ." : `Gói còn ${r.remaining} ảnh chưa có kết quả.`}
      </div>
      {issues.length > 0 && (
        <DataTable headers={["Ảnh số", "Loại", "Lý do"]}>
          {issues.map((x, i) => (
            <tr key={i}>
              <td className="mono">{x.position ?? ""}</td>
              <td>{x.kind}</td>
              <td>{x.reason}</td>
            </tr>
          ))}
        </DataTable>
      )}
    </div>
  );
}
