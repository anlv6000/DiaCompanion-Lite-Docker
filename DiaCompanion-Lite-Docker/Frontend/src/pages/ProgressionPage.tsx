import { useState } from "react";
import { useData } from "@/contexts/DataContext";
import { useAsync, useDebounce } from "@/lib/hooks";
import {
  PageHeader,
  Panel,
  Field,
  Button,
  LoadState,
  DataTable,
  GradeBadge,
} from "@/components/ui";
import { LineChart } from "@/components/charts";
import { fmtDate, num } from "@/lib/format";

export function ProgressionPage({
  patientId: initial,
}: {
  patientId?: number;
}) {
  const data = useData();
  const [q, setQ] = useState("");
  const dq = useDebounce(q);
  const [patientId, setPatientId] = useState(initial || 0);
  const [months, setMonths] = useState(initial ? 24 : 12);

  const patients = useAsync(
    () =>
      data.patients.list({
        q: dq.trim().length >= 2 ? dq : undefined,
        page: 1,
        pageSize: 20,
        sort: "name",
      }),
    [dq],
  );
  const prog = useAsync(
    () =>
      patientId
        ? data.diagnoses.progression(patientId, months)
        : Promise.resolve(null),
    [patientId, months],
  );

  const exportPng = () => {
    const svg = document.querySelector(".chart") as SVGElement | null;
    if (!svg) return;
    const text = new XMLSerializer().serializeToString(svg);
    const blob = new Blob([text], { type: "image/svg+xml" });
    const url = URL.createObjectURL(blob);
    const img = new Image();
    img.onload = () => {
      const c = document.createElement("canvas");
      c.width = 1560;
      c.height = 520;
      const ctx = c.getContext("2d");
      if (ctx) {
        ctx.fillStyle = "#fff";
        ctx.fillRect(0, 0, c.width, c.height);
        ctx.drawImage(img, 0, 0, c.width, c.height);
        c.toBlob((b) => {
          if (!b) return;
          const a = document.createElement("a");
          a.href = URL.createObjectURL(b);
          a.download = `progression-${patientId}.png`;
          a.click();
        }, "image/png");
      }
      URL.revokeObjectURL(url);
    };
    img.src = url;
  };

  return (
    <>
      <PageHeader
        title="Diễn tiến DR"
        subtitle="Ghép phân độ xác nhận, fractal dimension và HbA1c theo thời gian."
        // actions={
        //   <Button onClick={exportPng} disabled={!prog.data?.points.length}>
        //     Xuất PNG
        //   </Button>
        // }
      />
      {!initial && (
        <Panel>
          <div className="toolbar">
            <Field labelText="Tìm bệnh nhân" className="inline">
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Tên, mã hoặc SĐT"
              />
            </Field>
            <Field labelText="Chọn hồ sơ" className="inline">
              <select
                value={patientId || ""}
                onChange={(e) => setPatientId(Number(e.target.value))}
              >
                <option value="">Chọn bệnh nhân</option>
                {patients.data?.items.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.code} · {p.fullName}
                  </option>
                ))}
              </select>
            </Field>
            <Field labelText="Khoảng thời gian" className="inline">
              <select
                value={months}
                onChange={(e) => setMonths(Number(e.target.value))}
              >
                <option value="3">3 tháng</option>
                <option value="6">6 tháng</option>
                <option value="12">12 tháng</option>
                <option value="24">24 tháng</option>
                <option value="120">Tất cả</option>
              </select>
            </Field>
          </div>
        </Panel>
      )}
      <Panel title="Biểu đồ đa chuỗi">
        <LoadState
          loading={prog.loading}
          error={prog.error}
          empty={!patientId || !prog.data?.points.length}
          onRetry={prog.reload}
          emptyText={
            patientId
              ? "Chưa có kết quả đã xác nhận trong khoảng thời gian này."
              : "Chọn bệnh nhân để xem diễn tiến."
          }
        >
          {prog.data && (
            <>
              <LineChart
                series={[
                  {
                    name: "DR grade",
                    points: prog.data.points.map((p) => ({
                      x: p.date,
                      y: p.confirmedGrade,
                    })),
                  },
                  {
                    name: "Fractal",
                    kind: "defer",
                    points: prog.data.points.map((p) => ({
                      x: p.date,
                      y: p.fractalDimension,
                    })),
                  },
                  {
                    name: "HbA1c",
                    kind: "alert",
                    points: prog.data.points.map((p) => ({
                      x: p.date,
                      y: p.hbA1c,
                    })),
                  },
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
        <LoadState
          loading={prog.loading}
          error={prog.error}
          empty={!prog.data?.points.length}
        >
          <DataTable
            headers={["Ngày", "Lượt khám", "DR xác nhận", "Fractal", "HbA1c"]}
          >
            {prog.data?.points.map((p, i) => (
              <tr key={i}>
                <td className="mono">{fmtDate(p.date)}</td>
                <td className="mono">{p.visitId ? `#${p.visitId}` : "—"}</td>
                <td>
                  <GradeBadge grade={p.confirmedGrade} />
                </td>
                <td className="mono">{num(p.fractalDimension, 4)}</td>
                <td className="mono">
                  {p.hbA1c == null ? "—" : `${p.hbA1c}%`}
                </td>
              </tr>
            ))}
          </DataTable>
        </LoadState>
      </Panel>
    </>
  );
}
