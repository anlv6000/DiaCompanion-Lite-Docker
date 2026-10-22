import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useData } from "@/contexts/DataContext";
import { useAsync } from "@/lib/hooks";
import { Button, GradeBadge, LoadState, Panel, StatusBadge } from "@/components/ui";
import { ProtectedImage } from "@/components/ProtectedImage";
import { LineChart } from "@/components/charts";
import { diabetesTypes, genders, label, metricContexts } from "@/lib/enums";
import { fmtDate } from "@/lib/format";
import type { PatientDetailDto, VisitDto, VisitHealthMetricsDto } from "@/types/api";
import { EYE_NAME, REFERRAL, addMonths, bpOf, drText, finalGrade, isPlaceholderPhone, loadEntries, type Entry } from "@/lite/liteData";
import "@/lite/lite.css";

/* Hồ sơ bệnh án: gộp hành chính, bệnh ĐTĐ, tóm tắt hiện trạng, diễn tiến và toàn bộ
   các lần khám thành một trang đọc liền, in được. Số bệnh án = mã bệnh nhân. */

interface VisitRow {
  v: VisitDto;
  m: VisitHealthMetricsDto | null;
  entries: Entry[]; // mới nhất trước
  grade: (number | null)[]; // phân độ cuối cao nhất theo mắt trong lượt
}

export function PatientRecord({
  patient,
  visits,
  onGoVisit,
}: {
  patient: PatientDetailDto;
  visits: VisitDto[];
  onGoVisit: () => void;
}) {
  const data = useData();
  const navigate = useNavigate();
  const key = visits.map((v) => `${v.id}:${v.status}:${v.pendingReviewCount}:${v.imageCount}`).join(",");

  const rec = useAsync(async () => {
    const [entries, metrics] = await Promise.all([
      loadEntries(data, { patientId: patient.id }),
      Promise.all(visits.map((v) => data.visits.healthMetrics(v.id).catch(() => null))),
    ]);
    return { entries, metrics };
  }, [patient.id, key]);

  const rows: VisitRow[] = useMemo(() => {
    if (!rec.data) return [];
    return visits
      .map((v, i) => {
        const entries = rec.data!.entries
          .filter((e) => e.img.visitId === v.id)
          .sort((a, b) => b.img.id - a.img.id);
        const grade = [0, 1].map((eye) => {
          const gs = entries.filter((e) => e.img.eye === eye).map(finalGrade).filter((g): g is number => g != null);
          return gs.length ? Math.max(...gs) : null;
        });
        return { v, m: rec.data!.metrics[i], entries, grade };
      })
      .sort((a, b) => b.v.visitDate.localeCompare(a.v.visitDate));
  }, [rec.data, visits]);

  const [expanded, setExpanded] = useState<Set<number> | null>(null);
  const isOpen = (id: number) => (expanded ? expanded.has(id) : rows[0]?.v.id === id);
  const toggle = (id: number) =>
    setExpanded((prev) => {
      const s = new Set(prev ?? (rows[0] ? [rows[0].v.id] : []));
      s.has(id) ? s.delete(id) : s.add(id);
      return s;
    });
  const expandAll = (all: boolean) => setExpanded(new Set(all ? rows.map((r) => r.v.id) : []));

  const print = () => {
    expandAll(true);
    setTimeout(() => window.print(), 300);
  };

  const p = patient;
  return (
    <div className="lite-record">
      <div className="lite-rec-head">
        <div>
          <h2>Hồ sơ bệnh án — {p.code}</h2>
          <span className="faint lite-print-only">In ngày {fmtDate(new Date().toISOString(), true)}</span>
        </div>
        <div className="lite-actions no-print">
          <Button onClick={() => expandAll(!rows.every((r) => isOpen(r.v.id)))}>
            {rows.length && rows.every((r) => isOpen(r.v.id)) ? "Thu gọn tất cả" : "Mở tất cả"}
          </Button>
          <Button onClick={print} disabled={!rec.data}>In hồ sơ</Button>
        </div>
      </div>

      <Panel title="I. Hành chính">
        <div className="lite-kv">
          <KV k="Mã nghiên cứu" v={p.fullName} />
          <KV k="Giới" v={label(genders, p.gender)} />
          <KV k="Năm sinh" v={`${(p.dateOfBirth || "").slice(0, 4)} (${p.age} tuổi)`} mono />
          {!isPlaceholderPhone(p.phone) && <KV k="Số điện thoại" v={p.phone} mono />}
          {p.address && <KV k="Địa chỉ" v={p.address} />}
          <KV k="Bác sĩ phụ trách" v={p.doctorInCharge || "—"} />
          <KV k="Ngày tạo hồ sơ" v={fmtDate(p.createdAt)} mono />
        </div>
      </Panel>

      <Panel title="II. Bệnh đái tháo đường">
        <div className="lite-kv">
          <KV k="Loại" v={label(diabetesTypes, p.diabetesType)} />
          <KV k="Thời gian mắc" v={p.diabetesDurationYears != null ? `${p.diabetesDurationYears} năm` : "—"} mono />
          <KV k="HbA1c nền" v={p.baselineHbA1c != null ? `${p.baselineHbA1c}%` : "—"} mono />
          <KV k="Ghi chú" v={p.note || "—"} wide />
        </div>
      </Panel>

      {!rec.data ? (
        <LoadState loading={rec.loading} error={rec.error} onRetry={rec.reload}>{null}</LoadState>
      ) : (
        <>
          <Summary rows={rows} onGoVisit={onGoVisit} />
          <Trends rows={rows} />
          <Panel title={`V. Các lần khám (${rows.length})`}>
            {rows.length === 0 && <div className="empty"><b>Chưa có lần khám nào</b></div>}
            {rows.map((r) => (
              <VisitBlock
                key={r.v.id}
                row={r}
                open={isOpen(r.v.id)}
                onToggle={() => toggle(r.v.id)}
                onOpenImage={(e) => navigate(e.dx ? `/fundus/${e.img.id}?diagnosis=${e.dx.id}` : `/fundus/${e.img.id}`)}
                onGoVisit={onGoVisit}
              />
            ))}
          </Panel>
        </>
      )}
    </div>
  );
}

function KV({ k, v, mono, wide }: { k: string; v: string; mono?: boolean; wide?: boolean }) {
  return (
    <div className={wide ? "wide" : ""}>
      <span>{k}</span>
      <div className={mono ? "mono" : ""}>{v}</div>
    </div>
  );
}

/* ---------------------------------------------------------------- III. tóm tắt */
function Summary({ rows, onGoVisit }: { rows: VisitRow[]; onGoVisit: () => void }) {
  // Phân độ xác nhận gần nhất theo mắt
  const latestEye = [0, 1].map((eye) => {
    for (const r of rows) if (r.grade[eye] != null) return { g: r.grade[eye]!, date: r.v.visitDate };
    return null;
  });

  // Xu hướng: so mức cao nhất của hai lượt gần nhất có phân độ xác nhận
  const graded = rows.filter((r) => r.grade.some((g) => g != null));
  const maxOf = (r: VisitRow) => Math.max(...r.grade.filter((g): g is number => g != null));
  const trend =
    graded.length < 2 ? "Chưa đủ dữ liệu (cần ≥ 2 lần có phân độ)"
    : maxOf(graded[0]) > maxOf(graded[1]) ? "Nặng lên so với lần trước"
    : maxOf(graded[0]) < maxOf(graded[1]) ? "Cải thiện so với lần trước"
    : "Ổn định so với lần trước";
  const worse = graded.length >= 2 && maxOf(graded[0]) > maxOf(graded[1]);

  const lastHba1c = rows.find((r) => r.m?.hbA1c?.value != null);
  const lastBp = rows.find((r) => bpOf(r.m?.bloodPressure).sys != null);
  const bp = lastBp ? bpOf(lastBp.m?.bloodPressure) : null;

  const lastClosed = rows.find((r) => r.v.status !== 0 && r.v.closedAt);
  const due = lastClosed?.v.recheckMonths ? addMonths(lastClosed.v.closedAt!, lastClosed.v.recheckMonths) : null;
  const revisited = lastClosed && rows.some((r) => r.v.visitDate > lastClosed.v.closedAt!);
  const overdue = !!due && !revisited && due.getTime() < Date.now();

  const open = rows.find((r) => r.v.status === 0);

  return (
    <Panel title="III. Tóm tắt hiện trạng">
      <div className="lite-kv">
        {[0, 1].map((eye) => (
          <div key={eye}>
            <span>{EYE_NAME[eye]}</span>
            <div>
              {latestEye[eye] ? (
                <>
                  <GradeBadge grade={latestEye[eye]!.g} /> <span className="faint mono">({fmtDate(latestEye[eye]!.date)})</span>
                </>
              ) : "Chưa có phân độ xác nhận"}
            </div>
          </div>
        ))}
        <KV k="Xu hướng DR" v={trend} />
        <KV k="HbA1c gần nhất" v={lastHba1c ? `${lastHba1c.m!.hbA1c!.value}% (${fmtDate(lastHba1c.v.visitDate)})` : "—"} mono />
        <KV k="Huyết áp gần nhất" v={bp ? `${bp.sys}/${bp.dia ?? "—"} mmHg (${fmtDate(lastBp!.v.visitDate)})` : "—"} mono />
        <KV k="Chuyển tuyến (lần đóng gần nhất)" v={lastClosed ? label(REFERRAL, lastClosed.v.referral ?? 0) : "—"} />
        <div>
          <span>Tái khám dự kiến</span>
          <div className={overdue ? "lite-overdue mono" : "mono"}>
            {due ? `${fmtDate(due.toISOString())}${overdue ? " — quá hạn" : ""}` : "—"}
          </div>
        </div>
      </div>
      {worse && <div className="lite-note error">DR nặng lên so với lần khám trước.</div>}
      {open && (
        <div className="lite-note watch">
          Lượt ngày {fmtDate(open.v.visitDate)} đang mở
          {open.v.pendingReviewCount > 0 ? ` — ${open.v.pendingReviewCount} kết quả chờ xác nhận` : ""}.{" "}
          <button className="lite-linkbtn no-print" onClick={onGoVisit}>Đến lượt hiện tại →</button>
        </div>
      )}
    </Panel>
  );
}

/* ---------------------------------------------------------------- IV. diễn tiến */
function Trends({ rows }: { rows: VisitRow[] }) {
  const asc = [...rows].reverse();
  const x = (r: VisitRow) => fmtDate(r.v.visitDate);
  if (asc.length < 2) return null; // một lần khám thì biểu đồ không có ý nghĩa
  return (
    <Panel title="IV. Diễn tiến">
      <div className="lite-charts">
        <div className="lite-chart">
          <h4>Phân độ DR đã xác nhận (0–4)</h4>
          <LineChart
            series={[
              { name: "OD", points: asc.map((r) => ({ x: x(r), y: r.grade[0] })) },
              { name: "OS", kind: "defer", points: asc.map((r) => ({ x: x(r), y: r.grade[1] })) },
            ]}
          />
        </div>
        <div className="lite-chart">
          <h4>HbA1c (%)</h4>
          <LineChart series={[{ name: "HbA1c", kind: "alert", points: asc.map((r) => ({ x: x(r), y: r.m?.hbA1c?.value })) }]} />
        </div>
        <div className="lite-chart">
          <h4>Huyết áp (mmHg)</h4>
          <LineChart
            series={[
              { name: "Tâm thu", kind: "alert", points: asc.map((r) => ({ x: x(r), y: bpOf(r.m?.bloodPressure).sys })) },
              { name: "Tâm trương", points: asc.map((r) => ({ x: x(r), y: bpOf(r.m?.bloodPressure).dia })) },
            ]}
          />
        </div>
      </div>
    </Panel>
  );
}

/* ---------------------------------------------------------------- V. một lần khám */
function VisitBlock({
  row,
  open,
  onToggle,
  onOpenImage,
  onGoVisit,
}: {
  row: VisitRow;
  open: boolean;
  onToggle: () => void;
  onOpenImage: (e: Entry) => void;
  onGoVisit: () => void;
}) {
  const { v, m, entries, grade } = row;
  const bp = bpOf(m?.bloodPressure);
  const metricParts = [
    m?.hbA1c?.value != null ? `HbA1c ${m.hbA1c.value}%` : null,
    bp.sys != null ? `HA ${bp.sys}/${bp.dia ?? "—"} mmHg` : null,
    m?.glucose?.value != null
      ? `ĐH ${m.glucose.value} mmol/L${m.glucose.context ? ` (${label(metricContexts, m.glucose.context).toLowerCase()})` : ""}`
      : null,
  ].filter(Boolean);

  return (
    <div className="lite-visit">
      <button className="lite-visit-h" onClick={onToggle} aria-expanded={open}>
        <span className="mono">{open ? "▾" : "▸"} {fmtDate(v.visitDate)}</span>
        <span>{v.doctorName || "—"}</span>
        <StatusBadge text={v.status === 0 ? "Đang thu thập" : "Đã đóng"} kind={v.status === 0 ? "watch" : "ok"} />
        <span className="lite-visit-grades">
          OD <GradeBadge grade={grade[0]} /> · OS <GradeBadge grade={grade[1]} />
        </span>
      </button>

      {open && (
        <div className="lite-visit-b">
          <div className="lite-line"><span className="faint">Chỉ số:</span> {metricParts.length ? metricParts.join(" · ") : "không ghi nhận"}</div>

          {entries.length === 0 && <div className="lite-line faint">Không có ảnh.</div>}
          {entries.map((e) => (
            <div key={e.img.id} className="lite-eye-row">
              <ProtectedImage imageId={e.img.id} className="lite-thumb" alt={`${EYE_NAME[e.img.eye]} ${fmtDate(v.visitDate)}`} onClick={() => onOpenImage(e)} />
              <div className="lite-eye-row-text">
                <b>{EYE_NAME[e.img.eye]}</b>
                {e.blind && (
                  <div>
                    Chấm mù: <b>{e.blind.gradable ? drText(e.blind.grade) : "không chấm được"}</b>
                  </div>
                )}
                <div>
                  {e.img.qualityStatus === 2 ? (
                    <span className="faint">Ảnh không đạt chất lượng</span>
                  ) : !e.dx ? (
                    <span className="faint">Chưa phân tích AI</span>
                  ) : (
                    <>
                      AI: {drText(e.dx.drGrade)} →{" "}
                      {e.dx.isConfirmed && e.dx.review ? (
                        <>
                          Quyết định: <b>{drText(e.dx.review.finalGrade)}</b>{" "}
                          <span className={e.dx.review.finalGrade !== e.dx.drGrade ? "lite-diff" : "faint"}>
                            ({e.dx.review.actionLabel}{e.dx.review.doctorName ? `, ${e.dx.review.doctorName}` : ""})
                          </span>
                          {e.dx.review.reason && <div className="faint">Lý do: {e.dx.review.reason}</div>}
                        </>
                      ) : (
                        <span className="lite-diff">chưa xác nhận</span>
                      )}
                    </>
                  )}
                </div>
              </div>
              <span className="no-print"><Button size="sm" onClick={() => onOpenImage(e)}>Xem →</Button></span>
            </div>
          ))}

          {v.status === 0 ? (
            <div className="lite-line">
              Lượt đang thu thập. <button className="lite-linkbtn no-print" onClick={onGoVisit}>Đến lượt hiện tại →</button>
            </div>
          ) : (
            <div className="lite-line">
              <span className="faint">Kết luận:</span> {v.conclusion || "—"} · <span className="faint">Chuyển tuyến:</span>{" "}
              {label(REFERRAL, v.referral ?? 0)} · <span className="faint">Tái khám:</span>{" "}
              {v.recheckMonths ? `${v.recheckMonths} tháng` : "—"}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
