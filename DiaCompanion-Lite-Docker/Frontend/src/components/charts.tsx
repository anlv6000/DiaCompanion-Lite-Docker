import { gradeCodes, grades } from "@/lib/enums";

export function LineChart({
  series,
  xLabel,
  yLabel,
}: {
  series: {
    name: string;
    kind?: string;
    points: { x: string; y: number | null | undefined }[];
  }[];
  xLabel?: string;
  yLabel?: string;
}) {
  const all = series.flatMap((s) =>
    s.points.filter((p) => p.y != null).map((p) => Number(p.y)),
  );
  if (!all.length)
    return (
      <div className="empty">
        <b>Chưa có số liệu</b>Biểu đồ sẽ xuất hiện khi có dữ liệu phù hợp.
      </div>
    );

  const minRaw = Math.min(...all);
  const maxRaw = Math.max(...all);
  const min = Math.min(0, minRaw);
  const max = maxRaw === min ? min + 1 : maxRaw;
  const span = max - min || 1;
  const count = Math.max(...series.map((s) => s.points.length));
  const chartLeft = 56;
  const chartRight = 758;
  const chartTop = 28;
  const chartBottom = 220;
  const chartWidth = chartRight - chartLeft;
  const chartHeight = chartBottom - chartTop;

  const xAt = (i: number) => chartLeft + (i / Math.max(1, count - 1)) * chartWidth;
  const yAt = (value: number) => chartBottom - ((value - min) / span) * chartHeight;

  const path = (pts: { x: string; y: number | null | undefined }[]) => {
    let started = false;
    return pts
      .map((p, i) => {
        if (p.y == null) {
          started = false;
          return null;
        }
        const cmd = started ? "L" : "M";
        started = true;
        return `${cmd}${xAt(i)},${yAt(Number(p.y))}`;
      })
      .filter(Boolean)
      .join(" ");
  };

  const tickSource = series.find((s) => s.points.length === count)?.points ?? series[0].points;
  const tickIndexes = Array.from(
    new Set([0, Math.floor((count - 1) / 2), count - 1].filter((x) => x >= 0)),
  );

  return (
    <>
      <svg className="chart" viewBox="0 0 780 270" preserveAspectRatio="none">
        {[0, 0.5, 1].map((r) => {
          const y = chartBottom - r * chartHeight;
          const value = min + r * span;
          return (
            <g key={r}>
              <line className="grid-line" x1={chartLeft} y1={y} x2={chartRight} y2={y} />
              <text x="4" y={y + 3}>{value.toFixed(1)}</text>
            </g>
          );
        })}
        <line className="axis" x1={chartLeft} y1={chartBottom} x2={chartRight} y2={chartBottom} />
        <line className="axis" x1={chartLeft} y1={chartTop} x2={chartLeft} y2={chartBottom} />

        {tickIndexes.map((i) => (
          <g key={i}>
            <line className="axis-tick" x1={xAt(i)} y1={chartBottom} x2={xAt(i)} y2={chartBottom + 4} />
            <text x={xAt(i)} y={chartBottom + 17} textAnchor="middle">
              {tickSource[i]?.x ?? ""}
            </text>
          </g>
        ))}

        {series.map((s) => (
          <g key={s.name}>
            <path
              d={path(s.points)}
              className={
                s.kind === "defer"
                  ? "line-defer"
                  : s.kind === "alert"
                    ? "line-alert"
                    : "line-primary"
              }
            />
            {s.points.map((p, i) =>
              p.y == null ? null : (
                <circle
                  key={`${s.name}-${i}`}
                  className={
                    s.kind === "defer"
                      ? "point-defer"
                      : s.kind === "alert"
                        ? "point-alert"
                        : "point-primary"
                  }
                  cx={xAt(i)}
                  cy={yAt(Number(p.y))}
                  r="2.8"
                >
                  <title>{`${p.x}: ${Number(p.y).toFixed(2)}`}</title>
                </circle>
              ),
            )}
          </g>
        ))}

        {xLabel && <text className="axis-label" x={(chartLeft + chartRight) / 2} y="264" textAnchor="middle">{xLabel}</text>}
        {yLabel && (
          <text
            className="axis-label"
            x="14"
            y={(chartTop + chartBottom) / 2}
            textAnchor="middle"
            transform={`rotate(-90 14 ${(chartTop + chartBottom) / 2})`}
          >
            {yLabel}
          </text>
        )}
      </svg>
      <div className="legend">
        {series.map((s) => (
          <span className={s.kind || ""} key={s.name}>
            {s.name}
          </span>
        ))}
      </div>
    </>
  );
}

export function GradeBars({
  distribution,
}: {
  distribution: Record<string, number>;
}) {
  const safeDistribution = distribution ?? {};
  const vals = gradeCodes.map(
    (code, i) =>
      safeDistribution[code] ??
      safeDistribution[String(i)] ??
      0,
  );
  const max = Math.max(1, ...vals);
  return (
    <div className="bars">
      {vals.map((v, i) => (
        <div className="bar" key={i}>
          <span>{grades[i]}</span>
          <span style={{ background: "#eef0f3", height: 10 }}>
            <i
              style={{
                width: `${(v / max) * 100}%`,
                background: `var(--g${i})`,
              }}
            />
          </span>
          <b className="mono">{v}</b>
        </div>
      ))}
    </div>
  );
}
