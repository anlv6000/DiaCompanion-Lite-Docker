import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useData } from "@/contexts/DataContext";
import { useAsync, useDebounce } from "@/lib/hooks";
import {
  PageHeader,
  Panel,
  Field,
  Button,
  DataTable,
  LoadState,
  GradeBadge,
  EyeBadge,
  StatusBadge,
  Meter,
} from "@/components/ui";
import { useAuth } from "@/contexts/AuthContext";
import { hasRole } from "@/lib/roles";
import { useToast } from "@/contexts/ToastContext";
import { fmtDate, num } from "@/lib/format";
import { grades } from "@/lib/enums";
import type { TriageItemDto } from "@/types/api";

export function TriagePage() {
  const data = useData();
  const [doctor, setDoctor] = useState("");
  const [deferred, setDeferred] = useState("");
  const [q, setQ] = useState("");
  const dq = useDebounce(q);
  const [cursor, setCursor] = useState<string | undefined>(undefined);
  const [history, setHistory] = useState<(string | undefined)[]>([]);
  const [selected, setSelected] = useState<TriageItemDto | null>(null);

  const docs = useAsync(() => data.users.doctors(), []);
  const queue = useAsync(
    () =>
      data.triage.queue({
        doctorId: doctor,
        deferredOnly: deferred,
        cursor,
        q: dq,
        size: 25,
      }),
    [doctor, deferred, dq, cursor],
  );

  // Bỏ chọn nếu ca đang chọn không còn trong danh sách sau khi tải lại.
  useEffect(() => {
    if (
      selected &&
      !queue.data?.items.some((x) => x.aiDiagnosisId === selected.aiDiagnosisId)
    ) {
      setSelected(null);
    }
  }, [queue.data]); // eslint-disable-line react-hooks/exhaustive-deps

  const resetPaging = () => {
    setCursor(undefined);
    setHistory([]);
  };
  const next = () => {
    if (queue.data?.nextCursor) {
      setHistory((h) => [...h, cursor]);
      setCursor(queue.data.nextCursor!);
    }
  };
  const prev = () => {
    const h = [...history];
    const c = h.pop();
    setHistory(h);
    setCursor(c);
  };

  return (
    <>
      <PageHeader
        title="Hàng đợi triage"
        subtitle="Ưu tiên ca chuyển bác sĩ, cần chuyển tuyến và bất đồng cao."
        actions={<Button onClick={queue.reload}>Tải lại</Button>}
      />
      <div className="two-pane">
        <Panel title="Ca chờ xử lý">
          <div className="toolbar">
            <Field labelText="Phạm vi" className="inline">
              <select
                value={deferred}
                onChange={(e) => {
                  setDeferred(e.target.value);
                  resetPaging();
                }}
              >
                <option value="">Tất cả ca</option>
                <option value="true">Chỉ ca defer</option>
              </select>
            </Field>
            <Field labelText="Tìm ca" className="inline">
              <input
                value={q}
                onChange={(e) => {
                  setQ(e.target.value);
                  resetPaging();
                }}
                placeholder="Mã hoặc tên bệnh nhân"
              />
            </Field>
          </div>
          <LoadState
            loading={queue.loading}
            error={queue.error}
            empty={!queue.data?.items.length}
            onRetry={queue.reload}
          >
            <DataTable
              headers={[
                "Ca",
                "Bệnh nhân",
                "Mắt",
                "DR",
                "Nguy cơ",
                "Bất đồng",
                "Defer",
                "Chuyển tuyến",
                "Bác sĩ",
                "Thời điểm",
              ]}
            >
              {queue.data?.items.map((x) => (
                <tr
                  key={x.aiDiagnosisId}
                  className={
                    selected?.aiDiagnosisId === x.aiDiagnosisId
                      ? "sel clickable"
                      : "clickable"
                  }
                  onClick={() => setSelected(x)}
                >
                  <td className="mono">#{x.aiDiagnosisId}</td>
                  <td>
                    <b>{x.patientName}</b>
                    <div className="mono faint">{x.patientCode}</div>
                  </td>
                  <td>
                    <EyeBadge eye={x.eye} />
                  </td>
                  <td>
                    <GradeBadge grade={x.drGrade} />
                  </td>
                  <td className="mono">
                    {x.clinicalRiskScore == null ? "—" : x.clinicalRiskScore}
                  </td>
                  <td>
                    <Meter value={x.disagreement} kind="defer" />
                  </td>
                  <td>
                    {x.isDeferred ? (
                      <StatusBadge text="Chuyển bác sĩ" kind="defer" />
                    ) : (
                      <span className="faint">—</span>
                    )}
                  </td>
                  <td>
                    {x.needsReferral ? (
                      <StatusBadge text="Cần" kind="alert" />
                    ) : (
                      <StatusBadge text="Không" kind="ok" />
                    )}
                  </td>
                  <td>{x.doctorName || "—"}</td>
                  <td className="mono">{fmtDate(x.createdAt, true)}</td>
                </tr>
              ))}
            </DataTable>
            <div className="pagination">
              <span className="faint">
                Phân trang keyset — tránh bỏ sót ca mới.
              </span>
              <div className="actions">
                <Button disabled={!history.length} onClick={prev}>
                  Trước
                </Button>
                <Button disabled={!queue.data?.nextCursor} onClick={next}>
                  Tải tiếp
                </Button>
              </div>
            </div>
          </LoadState>
        </Panel>

        <div className="sticky-rail">
          {selected ? (
            <ReviewRail
              item={selected}
              onDone={() => {
                setSelected(null);
                queue.reload();
              }}
            />
          ) : (
            <Panel title="Duyệt kết quả">
              <div className="empty">
                <b>Chọn một ca</b>Bảng vẫn giữ nguyên khi panel duyệt mở bên
                phải.
              </div>
            </Panel>
          )}
        </div>
      </div>
    </>
  );
}

function ReviewRail({
  item,
  onDone,
}: {
  item: TriageItemDto;
  onDone: () => void;
}) {
  const { user } = useAuth();
  const data = useData();
  const navigate = useNavigate();
  const toast = useToast();
  const detail = useAsync(
    () => data.diagnoses.get(item.aiDiagnosisId),
    [item.aiDiagnosisId],
  );
  const [mode, setMode] = useState<"approve" | "override">("approve");
  const [grade, setGrade] = useState(item.drGrade);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);

  // Chỉ Bác sĩ được phê duyệt/ghi đè và truy cập hàng đợi triage.
  const canReview = hasRole(user, "Doctor");

  const submit = async () => {
    setBusy(true);
    try {
      if (mode === "approve")
        await data.triage.approve(item.aiDiagnosisId, item.rowVersion);
      else
        await data.triage.override(item.aiDiagnosisId, {
          rowVersion: item.rowVersion,
          finalGrade: grade,
          reason,
        });
      toast.push(
        mode === "approve"
          ? "Đã phê duyệt kết quả AI."
          : "Đã lưu phân độ ghi đè.",
        "success",
      );
      onDone();
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Panel
      title={`Duyệt ca #${item.aiDiagnosisId}`}
      action={
        <Button
          onClick={() =>
            navigate(
              `/fundus/${detail.data?.fundusImageId || ""}?diagnosis=${item.aiDiagnosisId}`,
            )
          }
          disabled={!detail.data}
        >
          Xem ảnh
        </Button>
      }
    >
      <LoadState
        loading={detail.loading}
        error={detail.error}
        empty={!detail.data}
        onRetry={detail.reload}
      >
        {detail.data && (
          <>
            <div className="detail-grid">
              <div>
                <small>AI grade</small>
                <div>
                  <GradeBadge grade={detail.data.drGrade} />
                </div>
              </div>
              <Info
                k="Nguy cơ nền"
                v={
                  detail.data.clinicalRiskScore == null
                    ? "—"
                    : `${detail.data.clinicalRiskScore} điểm`
                }
              />
              <Info k="Bất đồng" v={num(detail.data.disagreement, 3)} />
              <Info k="Fractal" v={num(detail.data.fractalDimension, 4)} />
              <Info k="Thời điểm" v={fmtDate(detail.data.createdAt, true)} />
            </div>
            {detail.data.isDeferred && (
              <div
                className="state"
                style={{
                  borderColor: "var(--defer)",
                  background: "var(--defer-bg)",
                  marginTop: 10,
                }}
              >
                <b>Ca được chuyển bác sĩ</b>
                <div>
                  {detail.data.deferReasonLabel || "Tín hiệu không chắc chắn"}
                </div>
              </div>
            )}

            {canReview ? (
              <>
                <div className="pill" style={{ margin: "12px 0" }}>
                  <button
                    className={mode === "approve" ? "on" : ""}
                    onClick={() => setMode("approve")}
                  >
                    Phê duyệt
                  </button>
                  <button
                    className={mode === "override" ? "on" : ""}
                    onClick={() => setMode("override")}
                  >
                    Ghi đè
                  </button>
                </div>
                {mode === "override" && (
                  <>
                    <Field labelText="Phân độ cuối" required>
                      <select
                        value={grade}
                        onChange={(e) => setGrade(Number(e.target.value))}
                      >
                        {grades.map((x, i) => (
                          <option key={i} value={i}>
                            {x}
                          </option>
                        ))}
                      </select>
                    </Field>
                    <Field labelText="Lý do ghi đè" required>
                      <textarea
                        value={reason}
                        onChange={(e) => setReason(e.target.value)}
                        placeholder="Lý do lâm sàng và quan sát trên ảnh"
                      />
                    </Field>
                  </>
                )}
                <Button
                  kind="primary"
                  busy={busy}
                  disabled={mode === "override" && !reason.trim()}
                  onClick={submit}
                >
                  {mode === "approve" ? "Phê duyệt kết quả" : "Lưu ghi đè"}
                </Button>
              </>
            ) : (
              <div className="help" style={{ marginTop: 12 }}>
                Chỉ tài khoản Bác sĩ được phê duyệt hoặc ghi đè kết quả AI.
              </div>
            )}
          </>
        )}
      </LoadState>
    </Panel>
  );
}

function Info({ k, v }: { k: string; v: React.ReactNode }) {
  return (
    <div>
      <small>{k}</small>
      <div className="mono">{v ?? "—"}</div>
    </div>
  );
}
