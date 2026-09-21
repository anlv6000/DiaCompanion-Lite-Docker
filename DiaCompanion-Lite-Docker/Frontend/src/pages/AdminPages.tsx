import { useState, useEffect } from "react";
import { useAsync } from "@/lib/hooks";
import { useData } from "@/contexts/DataContext";
import { useAuth } from "@/contexts/AuthContext";
import { hasRole } from "@/lib/roles";
import {
  PageHeader,
  Panel,
  Button,
  LoadState,
  DataTable,
  StatusBadge,
  GradeBadge,
  EyeBadge,
  Field,
  Modal,
  ConfirmDialog,
  Icon,
} from "@/components/ui";
import { GradeBars, LineChart } from "@/components/charts";
import { fmtDate, num, pct, downloadText, toCsv } from "@/lib/format";
import { useToast } from "@/contexts/ToastContext";
import type {
  SystemConfigDto,
  ThresholdImpactDto,
} from "@/types/api";

/* ---------------- Dashboard ---------------- */
export function DashboardPage() {
  const data = useData();
  const { user } = useAuth();
  const isAdmin = hasRole(user, "Admin");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const dash = useAsync(
    () =>
      data.admin.dashboard({
        from: from || undefined,
        to: to || undefined,
      }),
    [from, to],
  );
  const impacts = useAsyncImpacts(data, isAdmin);
  const filteredPeriod = Boolean(from || to);

  return (
    <>
      <PageHeader
        title="Dashboard thống kê"
        subtitle="Thống kê toàn hệ thống; một lượt AI chỉ chạy khi đủ mô hình DR, Lesion và Fractal đang hoạt động."
      />

      <Panel>
        <div className="toolbar">
          <Field labelText="Từ ngày" className="inline">
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </Field>
          <Field labelText="Đến ngày" className="inline">
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </Field>
          {(from || to) && (
            <Button onClick={() => { setFrom(""); setTo(""); }}>
              Xóa bộ lọc
            </Button>
          )}
        </div>
        {dash.data && (
          <div className="faint">
            Kỳ thống kê: <b>{dash.data.periodFrom}</b> → <b>{dash.data.periodTo}</b>
            {dash.data.scope === "AssignedDoctor" ? " · phạm vi bác sĩ phụ trách" : " · toàn hệ thống"}
          </div>
        )}
      </Panel>

      <LoadState
        loading={dash.loading}
        error={dash.error}
        empty={!dash.data}
        onRetry={dash.reload}
      >
        {dash.data && (
          <>
            <div className="stats">
              <Stat k={filteredPeriod ? "Bệnh nhân trong kỳ" : "Bệnh nhân hệ thống"} v={dash.data.totalPatients} />
              <Stat k="Lượt khám trong kỳ" v={dash.data.visitsThisMonth} />
              <Stat k="Chờ triage" v={dash.data.pendingTriage} />
              <Stat k="Defer chờ" v={dash.data.deferredPending} />
              <Stat k="Tỉ lệ defer" v={`${dash.data.deferralRate}%`} />
              <Stat k="Chuyển chuyên khoa" v={`${dash.data.referralRate}%`} />
              <Stat k="Ghi đè" v={`${dash.data.overrideRate}%`} />
            </div>

            <div className="grid2" style={{ marginTop: 12 }}>
              <Panel title="Phân bố mức DR đã được bác sĩ xác nhận">
                <GradeBars distribution={dash.data.gradeDistribution} />
              </Panel>
              <Panel title="Ngưỡng bất đồng → tỉ lệ defer ước tính">
                <LoadState
                  loading={impacts.loading}
                  error={impacts.error}
                  empty={!impacts.data?.length}
                  emptyText={
                    isAdmin
                      ? "Chưa có đủ ca để ước tính."
                      : "Chỉ Admin được truy vấn ảnh hưởng ngưỡng."
                  }
                >
                  <LineChart
                    xLabel="Ngưỡng bất đồng"
                    yLabel="Tỉ lệ defer (%)"
                    series={[
                      {
                        name: "Tỉ lệ defer dự kiến",
                        points: impacts.data || [],
                      },
                    ]}
                  />
                  {isAdmin && (
                    <div className="faint" style={{ marginTop: 6 }}>
                      Biểu đồ này sử dụng dữ liệu lịch sử toàn hệ thống để ước tính ảnh hưởng của ngưỡng;
                      không áp dụng bộ lọc ngày phía trên.
                    </div>
                  )}
                </LoadState>
              </Panel>
            </div>

          </>
        )}
      </LoadState>
    </>
  );
}

function useAsyncImpacts(data: ReturnType<typeof useData>, isAdmin: boolean) {
  return useAsync(async () => {
    if (!isAdmin) return [] as { x: string; y: number }[];
    const points: { x: string; y: number }[] = [];
    for (const proposed of [0.15, 0.2, 0.25, 0.3, 0.35, 0.4, 0.45, 0.5]) {
      try {
        const x = await data.admin.impact("ai.disagreement_threshold", proposed);
        points.push({ x: proposed.toFixed(1), y: x.projectedRate });
      } catch {
        /* bỏ qua điểm lỗi */
      }
    }
    return points;
  }, [isAdmin]);
}

function Stat({ k, v }: { k: string; v: React.ReactNode }) {
  return (
    <div className="stat">
      <span>{k}</span>
      <b className="mono">{v}</b>
    </div>
  );
}

/* ---------------- Conflicts ---------------- */
export function ConflictsPage() {
  const data = useData();
  const report = useAsync(() => data.exports.conflicts(), []);

  const download = async () => {
    const blob = await data.exports.conflictsCsv();
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = "disagreement-cases.csv";
    a.click();
  };

  const d = report.data;
  const cases = d?.cases || [];

  return (
    <>
      <PageHeader
        title="Ca người – máy mâu thuẫn"
        subtitle="Tập ca bác sĩ ghi đè dùng để đánh giá cơ chế deferral."
        actions={
          <Button onClick={download}>
            <Icon name="download" />
            Xuất CSV
          </Button>
        }
      />
      <LoadState
        loading={report.loading}
        error={report.error}
        empty={!d}
        onRetry={report.reload}
      >
        {d && (
          <>
            <div className="stats">
              <Stat k="Tổng đã duyệt" v={d.summary.totalReviewed} />
              <Stat k="Tổng ghi đè" v={d.summary.totalOverridden} />
              <Stat k="Tỉ lệ ghi đè" v={`${d.summary.overrideRate}%`} />
              <Stat
                k="Trong nhóm defer"
                v={`${d.summary.overrideRateWithinDeferred}%`}
              />
              <Stat
                k="Ngoài nhóm defer"
                v={`${d.summary.overrideRateOutsideDeferred}%`}
              />
              <Stat k="Bất đồng TB" v={num(d.summary.avgDisagreement, 4)} />
            </div>
            <div className="state" style={{ margin: "12px 0" }}>
              {d.summary.interpretation}
            </div>
            <Panel title="Danh sách ca">
              <DataTable
                headers={[
                  "Ca",
                  "Bệnh nhân",
                  "Mắt",
                  "AI",
                  "Bác sĩ",
                  "Lệch bậc",
                  "Bất đồng",
                  "Defer",
                  "Lý do",
                  "Ngày duyệt",
                ]}
              >
                {cases.map((x) => (
                  <tr key={x.aiDiagnosisId}>
                    <td className="mono">#{x.aiDiagnosisId}</td>
                    <td className="mono">{x.patientCode}</td>
                    <td>
                      <EyeBadge eye={x.eye} />
                    </td>
                    <td>
                      <GradeBadge grade={x.aiGrade} />
                    </td>
                    <td>
                      <GradeBadge grade={x.doctorGrade} />
                    </td>
                    <td className="mono">{x.gradeDistance}</td>
                    <td className="mono">{num(x.disagreement, 3)}</td>
                    <td>
                      {x.wasDeferred ? (
                        <StatusBadge text="Có" kind="defer" />
                      ) : (
                        <StatusBadge text="Không" />
                      )}
                    </td>
                    <td className="wrap-text">{x.reason || "—"}</td>
                    <td className="mono">{fmtDate(x.reviewedAt, true)}</td>
                  </tr>
                ))}
              </DataTable>
            </Panel>
          </>
        )}
      </LoadState>
    </>
  );
}

/* ---------------- Configs ---------------- */
export function ConfigsPage() {
  const data = useData();
  const toast = useToast();
  const list = useAsync(() => data.admin.configs(), []);
  const [edit, setEdit] = useState<SystemConfigDto | null>(null);

  const save = async (key: string, value: string) => {
    await data.admin.updateConfig(key, value, edit!.rowVersion);
    toast.push("Đã cập nhật cấu hình và ghi audit.", "success");
    setEdit(null);
    list.reload();
  };

  return (
    <>
      <PageHeader
        title="Cấu hình hệ thống"
        subtitle="Tham số nghiệp vụ; secret không được lưu ở đây."
      />
      <Panel>
        <LoadState
          loading={list.loading}
          error={list.error}
          empty={!list.data?.length}
          onRetry={list.reload}
        >
          <DataTable
            headers={[
              "Khóa",
              "Giá trị",
              "Kiểu",
              "Miền",
              "Mô tả",
              "Cập nhật",
              "Thao tác",
            ]}
          >
            {list.data?.map((c) => (
              <tr key={c.key}>
                <td className="mono">{c.key}</td>
                <td className="mono">{c.value}</td>
                <td>{c.valueType}</td>
                <td className="mono">
                  {c.minValue ?? "—"} … {c.maxValue ?? "—"}
                </td>
                <td className="wrap-text">{c.description || "—"}</td>
                <td className="mono">{fmtDate(c.updatedAt, true)}</td>
                <td>
                  <Button onClick={() => setEdit(c)}>Sửa</Button>
                </td>
              </tr>
            ))}
          </DataTable>
        </LoadState>
      </Panel>
      {edit && (
        <ConfigEditor
          value={edit}
          onClose={() => setEdit(null)}
          onSave={save}
        />
      )}
    </>
  );
}

function ConfigEditor({
  value,
  onClose,
  onSave,
}: {
  value: SystemConfigDto;
  onClose: () => void;
  onSave: (key: string, v: string) => void;
}) {
  const data = useData();
  const [v, setV] = useState(value.value);
  const [impact, setImpact] = useState<ThresholdImpactDto | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (
      !["ai.disagreement_threshold"].includes(
        value.key,
      )
    )
      return;
    const id = setTimeout(async () => {
      setBusy(true);
      try {
        setImpact(await data.admin.impact(value.key, Number(v)));
      } catch {
        /* ignore */
      } finally {
        setBusy(false);
      }
    }, 350);
    return () => clearTimeout(id);
  }, [v]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <Modal
      title={`Sửa ${value.key}`}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Hủy</Button>
          <Button kind="primary" onClick={() => onSave(value.key, v)}>
            Xác nhận & lưu
          </Button>
        </>
      }
    >
      <Field
        labelText="Giá trị"
        required
        help={`${value.valueType}; miền ${value.minValue ?? "—"}–${value.maxValue ?? "—"}`}
      >
        <input value={v} onChange={(e) => setV(e.target.value)} />
      </Field>
      <p>{value.description}</p>
      {busy && <div className="state">Đang ước tính ảnh hưởng…</div>}
      {impact && (
        <div className="credential">
          <div>
            Tỉ lệ defer hiện tại: <b>{impact.currentRate}%</b>
          </div>
          <div>
            Dự kiến sau thay đổi: <b>{impact.projectedRate}%</b>
          </div>
          <div>
            Số ca defer: {impact.currentDeferred} → {impact.projectedDeferred}
          </div>
          <p>{impact.note}</p>
        </div>
      )}
    </Modal>
  );
}

/* ---------------- Audit ---------------- */
export function AuditPage() {
  const data = useData();
  const [action, setAction] = useState("");
  const [entity, setEntity] = useState("");
  // BE /api/admin/audit nhận thêm entityId + userId; trước đây FE không dùng,
  // nên muốn tra "ai đã sửa hồ sơ số 128" phải cuộn tay qua toàn bộ nhật ký.
  const [entityId, setEntityId] = useState("");
  const [userId, setUserId] = useState("");
  const [cursor, setCursor] = useState<string | undefined>();
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [rows, setRows] = useState<any[]>([]);

  const page = useAsync(
    () =>
      data.admin.audit({
        action,
        entityType: entity,
        entityId: entityId || undefined,
        userId: userId || undefined,
        from: from ? new Date(from).toISOString() : undefined,
        to: to ? new Date(to + "T23:59:59").toISOString() : undefined,
        cursor,
        size: 50,
      }),
    [action, entity, entityId, userId, from, to, cursor],
  );

  useEffect(() => {
    if (page.data)
      setRows((r) => (cursor ? [...r, ...page.data!.items] : page.data!.items));
  }, [page.data]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    setCursor(undefined);
    setRows([]);
  }, [action, entity, entityId, userId, from, to]);

  const csv = () =>
    downloadText("audit.csv", toCsv(rows), "text/csv;charset=utf-8");

  return (
    <>
      <PageHeader
        title="Nhật ký audit"
        subtitle="Bản ghi chỉ đọc; bản thân audit chứa dữ liệu y tế và được kiểm soát quyền."
        actions={
          <Button onClick={csv} disabled={!rows.length}>
            Xuất CSV
          </Button>
        }
      />
      <Panel>
        <div className="toolbar">
          <Field labelText="Thao tác" className="inline">
            <input
              value={action}
              onChange={(e) => setAction(e.target.value.toUpperCase())}
              placeholder="LOGIN, OVERRIDE, VOID…"
            />
          </Field>
          <Field labelText="Loại đối tượng" className="inline">
            <input
              value={entity}
              onChange={(e) => setEntity(e.target.value)}
              placeholder="Patient, Visit…"
            />
          </Field>
          <Field labelText="Mã bản ghi" className="inline">
            <input
              inputMode="numeric"
              value={entityId}
              onChange={(e) => setEntityId(e.target.value.replace(/\D/g, ""))}
              placeholder="VD: 128"
            />
          </Field>
          <Field labelText="Người thực hiện (ID)" className="inline">
            <input
              inputMode="numeric"
              value={userId}
              onChange={(e) => setUserId(e.target.value.replace(/\D/g, ""))}
              placeholder="VD: 7"
            />
          </Field>
          <Field labelText="Từ ngày" className="inline">
            <input
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            />
          </Field>
          <Field labelText="Đến ngày" className="inline">
            <input
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
            />
          </Field>
        </div>
        <LoadState
          loading={page.loading && !rows.length}
          error={page.error}
          empty={!rows.length}
          onRetry={page.reload}
        >
          <DataTable
            headers={[
              "Thời điểm",
              "Người dùng",
              "Hành động",
              "Đối tượng",
              "ID",
              "Chi tiết",
              "IP",
              "Giá trị cũ/mới",
            ]}
          >
            {rows.map((x) => (
              <tr key={x.id}>
                <td className="mono">{fmtDate(x.createdAt, true)}</td>
                <td>{x.userName || "Hệ thống"}</td>
                <td>
                  <StatusBadge
                    text={x.action}
                    kind={
                      x.action.includes("VOID") || x.action.includes("FAILED")
                        ? "alert"
                        : ""
                    }
                  />
                </td>
                <td>{x.entityType}</td>
                <td className="mono">{x.entityId ?? "—"}</td>
                <td className="wrap-text">{x.detail || "—"}</td>
                <td className="mono">{x.ipAddress || "—"}</td>
                <td>
                  <details>
                    <summary>Xem JSON</summary>
                    <div className="code-block">
                      {x.oldValue || "—"}
                      {"\n→\n"}
                      {x.newValue || "—"}
                    </div>
                  </details>
                </td>
              </tr>
            ))}
          </DataTable>
        </LoadState>
        {page.data?.nextCursor && (
          <div className="pagination">
            <span />
            <Button
              onClick={() => setCursor(page.data?.nextCursor || undefined)}
            >
              Tải thêm
            </Button>
          </div>
        )}
      </Panel>
    </>
  );
}
