import {
  useEffect,
  useRef,
  useState,
  type CSSProperties,
  type PointerEvent as ReactPointerEvent,
  type ReactNode,
} from "react";
import { Button } from "@/components/ui";

const MIN = 0.5;
const MAX = 6;
const clamp = (v: number) => Math.min(MAX, Math.max(MIN, v));

/** Khung xem ảnh: phóng to/thu nhỏ (nút hoặc lăn chuột), giữ chuột kéo để di chuyển.
    Dùng chung cho trang kết quả AI và các màn chấm mù. */
export function PanZoomViewer({
  title,
  url,
  loading,
  error,
  emptyText = "Không có ảnh.",
  imageStyle,
  extraTool,
  className = "",
}: {
  title: string;
  url: string;
  loading: boolean;
  error: string;
  emptyText?: string;
  imageStyle?: CSSProperties;
  extraTool?: ReactNode;
  className?: string;
}) {
  const [zoom, setZoom] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const box = useRef<HTMLDivElement>(null);
  const drag = useRef<{ pointerId: number; startX: number; startY: number; originX: number; originY: number } | null>(null);

  const reset = () => {
    setZoom(1);
    setOffset({ x: 0, y: 0 });
  };
  useEffect(reset, [url]);

  const changeZoom = (next: number) => {
    const value = clamp(next);
    setZoom(value);
    if (value <= 1) setOffset({ x: 0, y: 0 });
  };

  // Lăn chuột để phóng to. Gắn listener không-passive để chặn cuộn trang.
  useEffect(() => {
    const el = box.current;
    if (!el || !url) return;
    const onWheel = (e: WheelEvent) => {
      e.preventDefault();
      setZoom((z) => {
        const value = clamp(z * (e.deltaY < 0 ? 1.15 : 1 / 1.15));
        if (value <= 1) setOffset({ x: 0, y: 0 });
        return value;
      });
    };
    el.addEventListener("wheel", onWheel, { passive: false });
    return () => el.removeEventListener("wheel", onWheel);
  }, [url]);

  const onPointerDown = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (!url || zoom <= 1) return;
    e.currentTarget.setPointerCapture(e.pointerId);
    drag.current = { pointerId: e.pointerId, startX: e.clientX, startY: e.clientY, originX: offset.x, originY: offset.y };
  };
  const onPointerMove = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (!drag.current || drag.current.pointerId !== e.pointerId) return;
    setOffset({
      x: drag.current.originX + e.clientX - drag.current.startX,
      y: drag.current.originY + e.clientY - drag.current.startY,
    });
  };
  const stopDrag = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (drag.current?.pointerId === e.pointerId) drag.current = null;
  };

  return (
    <section className={`panel ai-image-square ${className}`.trim()}>
      <div className="panel-h ai-square-header">
        <span>{title}</span>
        <span className="badge mono">{Math.round(zoom * 100)}%</span>
      </div>
      <div className="fundus-toolbar ai-square-toolbar">
        <Button onClick={() => changeZoom(zoom + 0.25)}>+</Button>
        <Button onClick={() => changeZoom(zoom - 0.25)}>−</Button>
        <Button onClick={reset}>100%</Button>
        {extraTool}
      </div>
      <div
        ref={box}
        className={`fundus ai-panzoom ${zoom > 1 ? "is-zoomed" : ""}`}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={stopDrag}
        onPointerCancel={stopDrag}
        onDoubleClick={() => (zoom > 1 ? reset() : changeZoom(2))}
      >
        {loading ? (
          <div>Đang tải ảnh…</div>
        ) : error ? (
          <div className="ai-image-error">{error}</div>
        ) : url ? (
          <img
            className="viewer-image"
            src={url}
            draggable={false}
            alt={title}
            style={{ transform: `translate3d(${offset.x}px, ${offset.y}px, 0) scale(${zoom})`, ...imageStyle }}
          />
        ) : (
          <div>{emptyText}</div>
        )}
      </div>
    </section>
  );
}

/** Bộ lọc red-free dùng chung (giống trang AI). */
export const RED_FREE: CSSProperties = { filter: "grayscale(1) contrast(1.3) sepia(.1) hue-rotate(65deg)" };
